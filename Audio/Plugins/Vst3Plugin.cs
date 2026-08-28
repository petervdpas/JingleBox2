using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// A loaded VST3 plugin with audio running through it: the host side of one insert slot.
/// </summary>
/// <remarks>
/// A VST3 plugin comes in two halves that barely know each other. The audio half holds the
/// busses and does the work; the settings half holds the knobs and the wording. They are made
/// separately, introduced to each other, and the audio half's state is copied across so the
/// knobs read what the sound is actually doing.
///
/// Two threads meet here, as in <see cref="ClapEffect"/>: everything but Process runs on the
/// UI thread, and everything the audio thread touches is allocated once at activation.
///
/// A knob move goes two places. The settings half is told at once, so what the host shows is
/// what the plugin thinks, and the value is queued for the audio half, which by the standard
/// can only be told at the start of a block. That means a move on an idle pad reaches the
/// sound on the first block it plays, which is soon enough to be inaudible and late enough to
/// be safe.
/// </remarks>
public sealed unsafe class Vst3Plugin : IPluginEffect, IPluginInstrument, IPluginWindowSource
{
    /// <summary>Stereo in, stereo out. Wider plugins are fed and read on their first two.</summary>
    public const int Channels = 2;

    /// <summary>How many knob moves one block can carry. A hand moves one at a time.</summary>
    private const int MaxChangesPerBlock = 64;

    /// <summary>
    /// How many notes one block can carry. A block is a few milliseconds, so this is a chord
    /// several times over.
    /// </summary>
    private const int MaxNotesPerBlock = 64;

    /// <summary>The bundle this plugin came out of. Held so the reference can be given back.</summary>
    private readonly Vst3Module _module;

    /// <summary>The audio half: busses, state, and being switched on.</summary>
    private readonly IComponent* _component;

    /// <summary>The same half's rendering face, which is where a block goes.</summary>
    private readonly IAudioProcessor* _processor;

    /// <summary>
    /// The settings half: the knobs, their wording, and the window. Null for a plugin that
    /// offers neither, which is rare and means no parameters and no picture.
    /// </summary>
    private readonly IEditController* _controller;

    /// <summary>
    /// Where the settings half reports a knob it moved itself. Native memory the plugin holds,
    /// carrying this instance's slot number rather than a managed pointer.
    /// </summary>
    private readonly void* _handler;

    /// <summary>True when the knobs live in the audio half rather than a class of their own.</summary>
    private readonly bool _sharedController;

    /// <summary>
    /// Held over the pending values, the queued notes and what is sounding, by the audio thread
    /// and the UI thread both.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Held while a patch is going into the plugin, and tried for while a block is going
    /// through it.
    /// </summary>
    /// <remarks>
    /// A patch arrives on whichever thread asked for the preset and a block arrives on the one
    /// filling the sound card, and the standard says these two may not happen at once: setting
    /// state is a plugin rebuilding what it plays with, wavetables and voices included, and
    /// doing that while another thread is halfway through reading them is undefined and, in a
    /// synth of any size, a crash.
    ///
    /// Tried for rather than waited on, on the audio thread's side. A block that arrives while
    /// a patch is going in comes out silent, which is what a plugin being reloaded sounds like
    /// anyway, and the alternative is holding up the sound card while somebody else's code
    /// reads twelve kilobytes of preset.
    /// </remarks>
    private readonly object _swapping = new();

    /// <summary>
    /// Knob moves waiting for the audio half, latest per parameter. A dictionary rather than a
    /// list because a knob dragged makes a hundred moves and only the last one matters.
    /// </summary>
    private readonly Dictionary<uint, double> _pending = new();

    /// <summary>The parameter moves handed to the plugin at the start of each block.</summary>
    private Vst3ParameterChanges? _changes;

    /// <summary>
    /// Somewhere for the plugin to put moves of its own. Nothing reads it back, but it has to be
    /// there and it has to answer: some plugins assert on a host that offers none.
    /// </summary>
    private Vst3ParameterChanges? _outgoing;

    /// <summary>The notes handed to the plugin at the start of each block.</summary>
    private Vst3EventList? _notes;

    /// <summary>Somewhere for the plugin to put notes of its own. Not read here.</summary>
    private Vst3EventList? _played;

    /// <summary>What is sounding, by pitch, so a note off can name the note that started.</summary>
    private readonly Dictionary<int, int> _sounding = new();

    /// <summary>Notes waiting for the next block, in the order they were played.</summary>
    private readonly List<(bool On, int Pitch, float Velocity, int Id)> _queued = new();

    /// <summary>
    /// The next identity to give a sounding note. Never reused, so the same pitch played twice
    /// at once is two notes the plugin can tell apart and end separately.
    /// </summary>
    private int _nextNoteId = 1;

    /// <summary>
    /// The channels each input bus carries, after the plugin has had its say about the
    /// arrangement. Read once at activation, since it cannot change while the plugin is on.
    /// </summary>
    private int[] _inputBusChannels = Array.Empty<int>();

    /// <inheritdoc cref="_inputBusChannels"/>
    private int[] _outputBusChannels = Array.Empty<int>();

    /// <summary>One description per input bus, pointing into the channels below.</summary>
    private AudioBusBuffers* _inputBusses;

    /// <inheritdoc cref="_inputBusses"/>
    private AudioBusBuffers* _outputBusses;

    /// <summary>One pointer per input channel, into <see cref="_inputData"/>. What VST3 reads.</summary>
    private float** _inputChannels;

    /// <inheritdoc cref="_inputChannels"/>
    private float** _outputChannels;

    /// <summary>
    /// One block of memory holding every input channel end to end. Allocated once at activation,
    /// because allocating inside an audio callback is how a mixer starts crackling.
    /// </summary>
    private float* _inputData;

    /// <inheritdoc cref="_inputData"/>
    private float* _outputData;

    /// <summary>What is handed to the plugin per block. Filled in at activation and reused.</summary>
    private ProcessData* _process;

    /// <summary>
    /// The largest block the plugin was set up for. A longer one from the device is fed through
    /// in pieces rather than refused.
    /// </summary>
    private int _maxFrames;

    /// <summary>True once the audio half has been switched on.</summary>
    private bool _active;

    /// <summary>True while the plugin has been told blocks are arriving.</summary>
    private bool _processing;

    /// <summary>
    /// True once this instance has been given up. A parked instance has it cleared again when
    /// somebody picks it up: see <see cref="TakeParked"/>.
    /// </summary>
    private bool _disposed;

    /// <summary>Which stack this instance goes back onto when it is given up.</summary>
    private string _key = "";

    /// <summary>
    /// Private because a plugin is only ever made by <see cref="Load"/>, which is the only place
    /// that knows both halves have been created, initialised and introduced.
    /// </summary>
    private Vst3Plugin(
        Vst3Module module,
        IComponent* component,
        IAudioProcessor* processor,
        IEditController* controller,
        void* handler,
        bool sharedController,
        PluginInfo info)
    {
        _module = module;
        _component = component;
        _processor = processor;
        _controller = controller;
        _handler = handler;
        _sharedController = sharedController;

        Info = info;
    }

    /// <inheritdoc/>
    public PluginInfo Info { get; }

    /// <inheritdoc/>
    public bool IsActive => _active;

    /// <summary>How many channels the plugin takes on its first input bus.</summary>
    public int InputChannels => _inputBusChannels.Length == 0 ? 0 : _inputBusChannels[0];

    /// <summary>How many channels the plugin gives on its first output bus.</summary>
    public int OutputChannels => _outputBusChannels.Length == 0 ? 0 : _outputBusChannels[0];

    /// <summary>How many audio busses the plugin has each way. A side chain is a bus of its own.</summary>
    public int InputBusses => _inputBusChannels.Length;

    /// <inheritdoc cref="InputBusses"/>
    public int OutputBusses => _outputBusChannels.Length;

    /// <summary>
    /// Opens one class out of a bundle and gets it ready to play. Null when the bundle will
    /// not load, does not hold that class, or refuses to start.
    /// </summary>
    /// <remarks>
    /// Every plugin is handed the host context at initialisation. It is the first thing Serum
    /// asks for, and a plugin that cannot find one may simply refuse to run.
    ///
    /// From the moment the handler is in place, a knob turned in the plugin's own window comes
    /// back to this instance, and so does a whole preset arriving.
    /// </remarks>
    public static Vst3Plugin? Load(string bundlePath, string classId, int sampleRate, int maxFrames)
    {
        string key = Key(bundlePath, classId, sampleRate, maxFrames);

        var parked = TakeParked(key);
        if (parked != null) return parked;

        var module = Vst3Module.Acquire(bundlePath);
        if (module == null) return null;

        var info = FindPlugin(module, classId);
        if (info == null)
        {
            module.Dispose();
            return null;
        }

        var component = module.CreateComponent(info.Id);
        if (component == null || component->Vtbl == null)
        {
            module.Dispose();
            return null;
        }

        if (component->Vtbl->Initialize(component, Vst3Host.Application()) != Vst3Abi.ResultOk)
        {
            Release(component);
            module.Dispose();
            return null;
        }

        var processor = (IAudioProcessor*)Query(component, Vst3Abi.AudioProcessorId);
        if (processor == null)
        {
            component->Vtbl->Terminate(component);
            Release(component);
            module.Dispose();
            return null;
        }

        var controller = OpenController(module, component, out bool shared);

        int slot = Vst3Host.NextSlot();
        void* handler = Vst3Host.CreateHandler(slot);

        if (controller != null)
        {
            controller->Vtbl->SetComponentHandler(controller, handler);
            Introduce(component, controller);
            CopyState(component, controller);
        }

        var effect = new Vst3Plugin(module, component, processor, controller, handler, shared, info) { _key = key, _slot = slot };

        Vst3Host.Listen(slot, effect.Moved);
        Vst3Host.ListenForReload(slot, effect.Reload);

        if (effect.Activate(sampleRate, maxFrames)) return effect;

        effect.Retire();
        return null;
    }

    /// <summary>
    /// Finds the settings half. Most plugins keep it in a class of its own and name that class;
    /// some put it in the same object as the audio half.
    /// </summary>
    private static IEditController* OpenController(Vst3Module module, IComponent* component, out bool shared)
    {
        shared = false;

        byte* classId = stackalloc byte[Vst3Abi.UidBytes];

        if (component->Vtbl->GetControllerClassId(component, classId) == Vst3Abi.ResultOk)
        {
            var separate = module.CreateController(classId);

            if (separate != null && separate->Vtbl != null)
            {
                if (separate->Vtbl->Initialize(separate, Vst3Host.Application()) == Vst3Abi.ResultOk) return separate;

                Release(separate);
            }
        }

        var same = (IEditController*)Query(component, Vst3Abi.EditControllerId);
        shared = same != null;

        return same;
    }

    /// <summary>
    /// Introduces the two halves so they can talk to each other. Plugins that keep a model in
    /// one half and a picture of it in the other need this or they drift apart.
    /// </summary>
    private static void Introduce(IComponent* component, IEditController* controller)
    {
        var fromAudio = (IConnectionPoint*)Query(component, Vst3Abi.ConnectionPointId);
        var fromSettings = (IConnectionPoint*)Query(controller, Vst3Abi.ConnectionPointId);

        if (fromAudio == null || fromSettings == null) return;

        fromAudio->Vtbl->Connect(fromAudio, fromSettings);
        fromSettings->Vtbl->Connect(fromSettings, fromAudio);
    }

    /// <summary>
    /// Takes the two halves apart again, which is the other half of introducing them.
    /// </summary>
    /// <remarks>
    /// A plugin left connected while its halves are being terminated is a plugin holding a
    /// pointer to something on its way out. Plugins say so out loud when it happens; DPF ones
    /// print an assertion about it on the way down.
    /// </remarks>
    private static void Part(IComponent* component, IEditController* controller)
    {
        if (component == null || controller == null) return;

        var fromAudio = (IConnectionPoint*)Query(component, Vst3Abi.ConnectionPointId);
        var fromSettings = (IConnectionPoint*)Query(controller, Vst3Abi.ConnectionPointId);

        if (fromAudio == null || fromSettings == null) return;

        fromAudio->Vtbl->Disconnect(fromAudio, fromSettings);
        fromSettings->Vtbl->Disconnect(fromSettings, fromAudio);
    }

    /// <summary>
    /// Copies what the audio half is set to over to the settings half, so the knobs open where
    /// the sound already is rather than at the factory defaults.
    /// </summary>
    /// <remarks>
    /// A plugin with nothing to say leaves the stream empty, and handing that back is what makes
    /// some of them assert on a read that returns nothing, so an empty one is not passed on.
    /// </remarks>
    private static void CopyState(IComponent* component, IEditController* controller)
    {
        using var state = new Vst3Stream();

        if (component->Vtbl->GetState(component, state.Pointer) != Vst3Abi.ResultOk) return;

        if (state.LooksEmpty) return;

        state.Rewind();
        controller->Vtbl->SetComponentState(controller, state.Pointer);
    }

    /// <summary>
    /// Which class in the bundle. No class given means the first one, which is what a bundle
    /// holding one plugin is and what a chain saved before ids were written down means.
    /// </summary>
    private static PluginInfo? FindPlugin(Vst3Module module, string classId)
    {
        var plugins = module.Plugins();
        if (plugins.Count == 0) return null;

        if (string.IsNullOrWhiteSpace(classId)) return plugins[0];

        foreach (var plugin in plugins)
        {
            if (string.Equals(plugin.Id, classId, StringComparison.OrdinalIgnoreCase)) return plugin;
        }

        return null;
    }

    /// <summary>
    /// Asks a VST3 object for another of its faces. Null for one it does not have, which is an
    /// ordinary answer rather than a fault. What comes back is already held and has to be
    /// released.
    /// </summary>
    private static void* Query(void* instance, byte[] id)
    {
        if (instance == null) return null;

        var unknown = (FUnknown*)instance;
        if (unknown->Vtbl == null) return null;

        void* result = null;

        fixed (byte* wanted = id)
        {
            if (unknown->Vtbl->QueryInterface(instance, wanted, &result) != Vst3Abi.ResultOk) return null;
        }

        return result;
    }

    /// <summary>
    /// Gives back one reference, guarding a null table, which a plugin that failed part way
    /// through construction can leave behind.
    /// </summary>
    private static void Release(void* instance)
    {
        if (instance == null) return;

        var unknown = (FUnknown*)instance;
        if (unknown->Vtbl != null && unknown->Vtbl->Release != null) unknown->Vtbl->Release(instance);
    }

    /// <summary>
    /// Agrees the busses, takes the memory the audio thread will need, and switches the plugin
    /// on for a rate and a block size.
    /// </summary>
    /// <remarks>
    /// Some plugins only accept being told that blocks are arriving once audio really is
    /// flowing, so a refusal there is not treated as fatal: the first block will find out.
    /// </remarks>
    private bool Activate(int sampleRate, int maxFrames)
    {
        _maxFrames = Math.Max(1, maxFrames);

        if (_processor->Vtbl->CanProcessSampleSize(_processor, Vst3Abi.Sample32) != Vst3Abi.ResultOk) return false;

        Arrange();
        Allocate(_maxFrames);

        var setup = new ProcessSetup
        {
            ProcessMode = Vst3Abi.RealtimeMode,
            SymbolicSampleSize = Vst3Abi.Sample32,
            MaxSamplesPerBlock = _maxFrames,
            SampleRate = sampleRate <= 0 ? 44100 : sampleRate
        };

        if (_processor->Vtbl->SetupProcessing(_processor, &setup) != Vst3Abi.ResultOk) return false;

        if (_component->Vtbl->SetActive(_component, 1) != Vst3Abi.ResultOk) return false;
        _active = true;

        if (_processor->Vtbl->SetProcessing(_processor, 1) != Vst3Abi.ResultOk)
        {
        }

        _processing = true;
        return true;
    }

    /// <summary>
    /// Agrees on what goes in and what comes out. Stereo is asked for everywhere and whatever
    /// the plugin settles on is read back, because a plugin is free to say no.
    /// </summary>
    /// <remarks>
    /// Only the main busses are switched on. An auxiliary bus is a side chain, and telling a
    /// plugin one is live when nothing is feeding it invites it to duck against silence.
    ///
    /// Every event bus is switched on, both ways. Notes arrive on one, and a bus nobody switched
    /// on is a bus the plugin ignores: that is the difference between an instrument that plays
    /// and one that sits there taking every note without a sound.
    /// </remarks>
    private void Arrange()
    {
        int inputs = Math.Max(0, _component->Vtbl->GetBusCount(_component, Vst3Abi.MediaAudio, Vst3Abi.DirectionInput));
        int outputs = Math.Max(0, _component->Vtbl->GetBusCount(_component, Vst3Abi.MediaAudio, Vst3Abi.DirectionOutput));

        var wantedIn = new ulong[Math.Max(1, inputs)];
        var wantedOut = new ulong[Math.Max(1, outputs)];

        for (int bus = 0; bus < inputs; bus++) wantedIn[bus] = Vst3Abi.StereoArrangement;
        for (int bus = 0; bus < outputs; bus++) wantedOut[bus] = Vst3Abi.StereoArrangement;

        fixed (ulong* ins = wantedIn)
        fixed (ulong* outs = wantedOut)
        {
            _processor->Vtbl->SetBusArrangements(_processor, ins, inputs, outs, outputs);
        }

        _inputBusChannels = Read(inputs, Vst3Abi.DirectionInput);
        _outputBusChannels = Read(outputs, Vst3Abi.DirectionOutput);

        SwitchOnMainBusses(inputs, Vst3Abi.DirectionInput);
        SwitchOnMainBusses(outputs, Vst3Abi.DirectionOutput);

        SwitchOnEventBusses(Vst3Abi.DirectionInput);
        SwitchOnEventBusses(Vst3Abi.DirectionOutput);
    }

    /// <summary>
    /// What each bus ended up carrying. The arrangement is asked for first, since that is what
    /// the plugin actually agreed to, and the channel count is a mask of speakers so the answer
    /// is how many bits are set in it. A plugin that will not say falls back to the bus's own
    /// description.
    /// </summary>
    private int[] Read(int count, int direction)
    {
        var channels = new int[count];

        for (int bus = 0; bus < count; bus++)
        {
            ulong arrangement = 0;

            if (_processor->Vtbl->GetBusArrangement(_processor, direction, bus, &arrangement) == Vst3Abi.ResultOk &&
                arrangement != 0)
            {
                channels[bus] = System.Numerics.BitOperations.PopCount(arrangement);
                continue;
            }

            var info = new BusInfo();
            channels[bus] = _component->Vtbl->GetBusInfo(_component, Vst3Abi.MediaAudio, direction, bus, &info) == Vst3Abi.ResultOk
                ? Math.Max(0, info.ChannelCount)
                : 0;
        }

        return channels;
    }

    /// <summary>
    /// Switches the main audio busses on and everything else off, in one direction. Called with
    /// the bus count rather than asking again, since the count is settled by this point.
    /// </summary>
    private void SwitchOnMainBusses(int count, int direction)
    {
        var info = new BusInfo();

        for (int bus = 0; bus < count; bus++)
        {
            if (_component->Vtbl->GetBusInfo(_component, Vst3Abi.MediaAudio, direction, bus, &info) != Vst3Abi.ResultOk) continue;

            byte state = (byte)(info.BusType == Vst3Abi.BusMain ? 1 : 0);
            _component->Vtbl->ActivateBus(_component, Vst3Abi.MediaAudio, direction, bus, state);
        }
    }

    /// <summary>
    /// Switches every event bus on in one direction, main or not: an instrument with its note
    /// bus off takes every note and makes no sound.
    /// </summary>
    private void SwitchOnEventBusses(int direction)
    {
        int count = _component->Vtbl->GetBusCount(_component, Vst3Abi.MediaEvent, direction);

        for (int bus = 0; bus < count; bus++)
        {
            _component->Vtbl->ActivateBus(_component, Vst3Abi.MediaEvent, direction, bus, 1);
        }
    }

    /// <summary>Everything the audio thread needs, taken once so it never allocates.</summary>
    private void Allocate(int frames)
    {
        _inputBusses = AllocBusses(_inputBusChannels, frames, out _inputData, out _inputChannels);
        _outputBusses = AllocBusses(_outputBusChannels, frames, out _outputData, out _outputChannels);

        _changes = new Vst3ParameterChanges(MaxChangesPerBlock);
        _outgoing = new Vst3ParameterChanges(MaxChangesPerBlock);

        _notes = new Vst3EventList(MaxNotesPerBlock);
        _played = new Vst3EventList(MaxNotesPerBlock);

        _process = Alloc<ProcessData>(1);
        _process->ProcessMode = Vst3Abi.RealtimeMode;
        _process->SymbolicSampleSize = Vst3Abi.Sample32;
        _process->NumInputs = _inputBusChannels.Length;
        _process->NumOutputs = _outputBusChannels.Length;
        _process->Inputs = _inputBusses;
        _process->Outputs = _outputBusses;
        _process->InputParameterChanges = _changes.Pointer;
        _process->OutputParameterChanges = _outgoing.Pointer;
        _process->InputEvents = _notes.Pointer;
        _process->OutputEvents = _played.Pointer;
        _process->ProcessContext = null;
    }

    /// <summary>
    /// One flat block of samples for every channel of every bus, with the pointer arrays that
    /// carve it up. Every bus the plugin declared gets its own room, whether anything is going
    /// to be put in it or not, because a plugin handed fewer busses than it asked for is a
    /// plugin reading past the end of the list.
    /// </summary>
    /// <remarks>
    /// The pointer array is asked for in bytes rather than through the generic helper, because a
    /// pointer is not something a generic type argument can be.
    /// </remarks>
    private AudioBusBuffers* AllocBusses(int[] busses, int frames, out float* data, out float** pointers)
    {
        int total = 0;
        foreach (int channels in busses) total += Math.Max(0, channels);

        data = Alloc<float>(Math.Max(1, total * frames));
        pointers = (float**)NativeMemory.AllocZeroed((nuint)Math.Max(1, total), (nuint)sizeof(float*));

        var buffers = Alloc<AudioBusBuffers>(Math.Max(1, busses.Length));

        int index = 0;

        for (int bus = 0; bus < busses.Length; bus++)
        {
            int channels = Math.Max(0, busses[bus]);

            buffers[bus].NumChannels = channels;
            buffers[bus].SilenceFlags = 0;
            buffers[bus].ChannelBuffers32 = channels == 0 ? null : pointers + index;

            for (int channel = 0; channel < channels; channel++)
            {
                pointers[index] = data + (long)index * frames;
                index++;
            }
        }

        return buffers;
    }

    /// <summary>
    /// Unmanaged memory, zeroed, and never nought bytes of it: a null pointer handed to a plugin
    /// is a read through nothing on its first block.
    /// </summary>
    private static T* Alloc<T>(int count) where T : unmanaged
    {
        return (T*)NativeMemory.AllocZeroed((nuint)Math.Max(1, count), (nuint)sizeof(T));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A block that arrives while a patch is going in is given up rather than waited for, and
    /// what comes out is silence, which is what the plugin has to give at that moment in any
    /// case. See <see cref="_swapping"/>.
    ///
    /// BASS asks for its whole playback buffer the first time it fills one, which is far more
    /// than a plugin was set up for, so the block is cut to what the plugin agreed to.
    /// </remarks>
    public void Process(float[] buffer, int frames)
    {
        if (_disposed || !_active || buffer == null || frames <= 0) return;
        if (_outputBusChannels.Length == 0) return;

        if (!System.Threading.Monitor.TryEnter(_swapping))
        {
            Array.Clear(buffer, 0, Math.Min(buffer.Length, frames * 2));
            return;
        }

        try
        {
            int offset = 0;

            while (offset < frames)
            {
                int chunk = Math.Min(_maxFrames, frames - offset);
                ProcessBlock(buffer, offset, chunk);
                offset += chunk;
            }
        }
        finally
        {
            System.Threading.Monitor.Exit(_swapping);
        }
    }

    /// <summary>
    /// One block no longer than the plugin was set up for.
    /// </summary>
    /// <remarks>
    /// The track goes into the main bus. Everything else the plugin declared, a side chain
    /// included, is given silence rather than whatever happened to be left in it last block.
    ///
    /// Mono in takes the two sides summed rather than the left one alone, so a signal panned
    /// right does not vanish into it; mono out goes to both sides, or half the mixer would go
    /// quiet.
    /// </remarks>
    private void ProcessBlock(float[] buffer, int offset, int frames)
    {
        int fed = 0;

        if (InputChannels == 1)
        {
            for (int frame = 0; frame < frames; frame++)
                _inputChannels[0][frame] = (buffer[(offset + frame) * 2] + buffer[(offset + frame) * 2 + 1]) * 0.5f;

            fed = 1;
        }
        else if (InputChannels >= 2)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                _inputChannels[0][frame] = buffer[(offset + frame) * 2];
                _inputChannels[1][frame] = buffer[(offset + frame) * 2 + 1];
            }

            fed = 2;
        }

        int channels = 0;
        foreach (int count in _inputBusChannels) channels += count;

        for (int channel = fed; channel < channels; channel++)
        {
            for (int frame = 0; frame < frames; frame++) _inputChannels[channel][frame] = 0;
        }

        Silence(_inputBusses, _inputBusChannels, fed);

        TakePending();
        TakeNotes();

        _process->NumSamples = frames;

        _processor->Vtbl->Process(_processor, _process);

        _changes?.Clear();
        _outgoing?.Clear();
        _notes?.Clear();
        _played?.Clear();

        int given = OutputChannels;

        if (given >= 2)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                buffer[(offset + frame) * 2] = _outputChannels[0][frame];
                buffer[(offset + frame) * 2 + 1] = _outputChannels[1][frame];
            }
        }
        else if (given == 1)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                float mono = _outputChannels[0][frame];
                buffer[(offset + frame) * 2] = mono;
                buffer[(offset + frame) * 2 + 1] = mono;
            }
        }
    }

    /// <summary>
    /// Marks which channels have nothing in them. A plugin that reads this can skip a silent
    /// side chain rather than working through a block of zeroes.
    /// </summary>
    private void Silence(AudioBusBuffers* busses, int[] channels, int fed)
    {
        int seen = 0;

        for (int bus = 0; bus < channels.Length; bus++)
        {
            int count = channels[bus];
            ulong flags = 0;

            for (int channel = 0; channel < count && channel < 64; channel++)
            {
                if (seen + channel >= fed) flags |= 1UL << channel;
            }

            busses[bus].SilenceFlags = flags;
            seen += count;
        }
    }

    /// <summary>Takes whatever the knobs have queued and attaches it to this block.</summary>
    private void TakePending()
    {
        if (_changes == null) return;

        lock (_lock)
        {
            if (_pending.Count == 0) return;

            foreach (var (id, value) in _pending)
            {
                if (!_changes.Add(id, value)) break;
            }

            _pending.Clear();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Normalised: every VST3 value is nought to one whatever it means, which is why the range
    /// is written as nought to one here and the wording has to be asked of the plugin.
    ///
    /// Program changes are left out. They are a list of patches rather than a knob, and this
    /// host has no way to show one yet; drawn as a dial, one would load a preset per pixel.
    /// </remarks>
    public IReadOnlyList<PluginParameter> Parameters()
    {
        var parameters = new List<PluginParameter>();
        if (_disposed || _controller == null) return parameters;

        int count = _controller->Vtbl->GetParameterCount(_controller);
        var info = new ParameterInfo();

        for (int index = 0; index < count; index++)
        {
            if (_controller->Vtbl->GetParameterInfo(_controller, index, &info) != Vst3Abi.ResultOk) continue;

            if ((info.Flags & Vst3Abi.ProgramChangeFlag) != 0) continue;

            parameters.Add(new PluginParameter(
                info.Id,
                ReadWide(info.Title),
                0,
                1,
                info.DefaultNormalizedValue,
                Math.Max(0, info.StepCount),
                (info.Flags & Vst3Abi.HiddenFlag) != 0,
                (info.Flags & Vst3Abi.ReadOnlyFlag) != 0,
                (info.Flags & Vst3Abi.BypassFlag) != 0,
                Normalized: true,
                Units: ReadWide(info.Units)));
        }

        return parameters;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// From the settings half, which is the half that holds the value a person set. The audio
    /// half has no way to be asked.
    /// </remarks>
    public double ValueOf(uint id)
    {
        if (_disposed || _controller == null) return 0;

        return _controller->Vtbl->GetParamNormalized(_controller, id);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The only way to print a VST3 parameter at all, since the number is nought to one whatever
    /// it means. The buffer is a String128, which is the size the interface fixes.
    /// </remarks>
    public string TextFor(uint id, double value)
    {
        if (_disposed || _controller == null) return "";

        char* text = stackalloc char[128];

        if (_controller->Vtbl->GetParamStringByValue(_controller, id, value, text) != Vst3Abi.ResultOk) return "";

        return ReadWide((byte*)text);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The settings half hears it now, so what is shown is what the plugin believes; the audio
    /// half hears it on its next block, which is the only time VST3 allows. A host that writes
    /// to one and not the other leaves a plugin whose window and whose sound disagree.
    /// </remarks>
    public void SetValue(uint id, double value)
    {
        if (_disposed) return;

        double clamped = Math.Clamp(value, 0, 1);

        if (_controller != null) _controller->Vtbl->SetParamNormalized(_controller, id, clamped);

        lock (_lock) _pending[id] = clamped;
    }

    /// <summary>Which slot this plugin answers on, for knob moves coming back from its window.</summary>
    private int _slot;

    /// <inheritdoc/>
    public event Action<uint, double>? Edited;

    /// <inheritdoc/>
    public event Action? Reloaded;

    /// <summary>The plugin says everything about it may have changed, which is a preset.</summary>
    internal void Reload()
    {
        if (_disposed) return;

        Reloaded?.Invoke();
    }

    /// <summary>
    /// A knob turned in the plugin's own window.
    /// </summary>
    /// <remarks>
    /// The half that draws already knows; it is the half that plays that has not heard, and in
    /// VST3 the only road between them is through here. Queued rather than written, like every
    /// other parameter move, because a plugin hears about values at the start of a block.
    /// </remarks>
    internal void Moved(uint id, double value)
    {
        if (_disposed) return;

        double clamped = Math.Clamp(value, 0, 1);

        lock (_lock) _pending[id] = clamped;

        Edited?.Invoke(id, clamped);
    }

    /// <summary>
    /// Nothing to do. VST3 has no way to hand a plugin a value outside of a block, so the
    /// queue simply waits, and the settings half already knows. This is what stops a knob on
    /// an idle pad from springing back.
    /// </summary>
    /// <inheritdoc/>
    public void FlushParameters()
    {
    }

    /// <summary>
    /// Starts a note. Queued rather than played: a plugin hears about notes at the start of a
    /// block, on the audio thread, not when a key goes down.
    /// </summary>
    /// <inheritdoc/>
    /// <remarks>
    /// Queued rather than played: a plugin hears about notes at the start of a block, on the
    /// audio thread, not when a key goes down.
    ///
    /// The same pitch twice over is the tracker retriggering a note. The one that was sounding
    /// is ended first, or the plugin is left holding a note nothing will ever release.
    /// </remarks>
    public void NoteOn(int semitone, float velocity)
    {
        if (_disposed) return;

        int pitch = Math.Clamp(semitone, 0, 127);

        lock (_lock)
        {
            if (_sounding.TryGetValue(pitch, out int held))
            {
                _queued.Add((false, pitch, 0, held));
                _sounding.Remove(pitch);
            }

            int id = _nextNoteId++;

            _sounding[pitch] = id;
            _queued.Add((true, pitch, Math.Clamp(velocity, 0, 1), id));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A note off for something that never started is a note off from somewhere that lost track,
    /// and passing it on would end a note the plugin is holding for somebody else.
    /// </remarks>
    public void NoteOff(int semitone)
    {
        if (_disposed) return;

        int pitch = Math.Clamp(semitone, 0, 127);

        lock (_lock)
        {
            if (!_sounding.TryGetValue(pitch, out int held)) return;

            _sounding.Remove(pitch);
            _queued.Add((false, pitch, 0, held));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every sounding note is turned into a note off in the queue rather than being cut, so the
    /// plugin's own releases run and a reverb tail is not chopped in half.
    /// </remarks>
    public void AllNotesOff()
    {
        if (_disposed) return;

        lock (_lock)
        {
            foreach (var (pitch, held) in _sounding) _queued.Add((false, pitch, 0, held));

            _sounding.Clear();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The same path as an effect's. An instrument has no audio input, so what was in the buffer
    /// goes in and is written over by what comes out, which amounts to replacing it.
    /// </remarks>
    public void Render(float[] buffer, int frames)
    {
        if (buffer == null || frames <= 0) return;

        Process(buffer, frames);
    }

    /// <summary>Hands the waiting notes to this block.</summary>
    private void TakeNotes()
    {
        if (_notes == null) return;

        lock (_lock)
        {
            if (_queued.Count == 0) return;

            foreach (var (on, pitch, velocity, id) in _queued)
            {
                bool room = on
                    ? _notes.NoteOn(pitch, velocity, id)
                    : _notes.NoteOff(pitch, 0, id);

                if (!room) break;
            }

            _queued.Clear();
        }
    }

    /// <summary>
    /// Everything inside the plugin, as a lump to keep. This is where a patch really lives:
    /// the parameters are knob positions, and a wavetable is not a knob position.
    /// </summary>
    /// <remarks>
    /// Reading what a plugin holds is the same kind of reach into it as writing it, and it
    /// happens while a song is being saved, which is a thing people do while something is
    /// playing. So it takes the same lock a patch going in does.
    /// </remarks>
    public byte[] SaveState()
    {
        if (_disposed || _component == null) return Array.Empty<byte>();

        lock (_swapping)
        {
            return SaveStateHere();
        }
    }

    /// <summary>
    /// Reads both halves, with the lock already held.
    /// </summary>
    /// <remarks>
    /// The settings half keeps its own state, and it is not the same lump as the audio half's.
    /// What is in it is everything the plugin shows rather than everything it plays: which
    /// preset is on, what the browser is looking at, where the panels are. Saving only the audio
    /// half is a song that comes back sounding right and looking like nothing was ever loaded,
    /// which is what Serum saying "- Init -" over the patch you chose actually is.
    /// </remarks>
    private byte[] SaveStateHere()
    {
        using var state = new Vst3Stream();

        if (_component->Vtbl->GetState(_component, state.Pointer) != Vst3Abi.ResultOk) return Array.Empty<byte>();
        if (state.LooksEmpty) return Array.Empty<byte>();

        var sound = state.ToArray();

        if (_controller == null) return sound;

        using var settings = new Vst3Stream();

        if (_controller->Vtbl->GetState(_controller, settings.Pointer) != Vst3Abi.ResultOk)
            return sound;

        if (settings.LooksEmpty) return sound;

        return Together(sound, settings.ToArray());
    }

    /// <summary>What marks a lump as holding both halves rather than only the audio one.</summary>
    /// <remarks>
    /// Songs saved before the settings half was kept hold the audio half on its own, and they
    /// have to go on loading. So the two-part form is marked, and anything without the mark is
    /// read the old way: all of it is the audio half. No plugin's own state can be mistaken
    /// for the mark, because a plugin's state never has to start with these eight bytes and
    /// the length that follows has to add up as well.
    /// </remarks>
    private static readonly byte[] BothHalves = "JB3STATE"u8.ToArray();

    /// <summary>The two halves in one lump, marked so it can be told apart from the old form.</summary>
    private static byte[] Together(byte[] sound, byte[] settings)
    {
        var lump = new byte[BothHalves.Length + 8 + sound.Length + settings.Length];

        Buffer.BlockCopy(BothHalves, 0, lump, 0, BothHalves.Length);
        BitConverter.TryWriteBytes(lump.AsSpan(BothHalves.Length, 4), sound.Length);
        BitConverter.TryWriteBytes(lump.AsSpan(BothHalves.Length + 4, 4), settings.Length);

        Buffer.BlockCopy(sound, 0, lump, BothHalves.Length + 8, sound.Length);
        Buffer.BlockCopy(settings, 0, lump, BothHalves.Length + 8 + sound.Length, settings.Length);

        return lump;
    }

    /// <summary>
    /// Takes a lump apart into the audio half and the settings half. A lump saved before the
    /// settings half was kept is all audio half, which is what the old songs are.
    /// </summary>
    private static (byte[] Sound, byte[] Settings) Apart(byte[] lump)
    {
        if (lump.Length < BothHalves.Length + 8) return (lump, Array.Empty<byte>());

        for (int index = 0; index < BothHalves.Length; index++)
        {
            if (lump[index] != BothHalves[index]) return (lump, Array.Empty<byte>());
        }

        int sound = BitConverter.ToInt32(lump, BothHalves.Length);
        int settings = BitConverter.ToInt32(lump, BothHalves.Length + 4);

        if (sound < 0 || settings < 0) return (lump, Array.Empty<byte>());
        if (BothHalves.Length + 8 + (long)sound + settings != lump.Length) return (lump, Array.Empty<byte>());

        var one = new byte[sound];
        var two = new byte[settings];

        Buffer.BlockCopy(lump, BothHalves.Length + 8, one, 0, sound);
        Buffer.BlockCopy(lump, BothHalves.Length + 8 + sound, two, 0, settings);

        return (one, two);
    }

    /// <summary>
    /// Puts a saved lump back, into both halves: the audio half so it sounds right, and the
    /// settings half so the knobs agree with it.
    /// </summary>
    /// <remarks>
    /// The block that is in the plugin now is allowed to finish; the next one goes silent until
    /// this is done. See <see cref="_swapping"/>.
    /// </remarks>
    public void LoadState(byte[]? state)
    {
        if (_disposed || _component == null || state == null || state.Length == 0) return;

        lock (_swapping)
        {
            LoadStateHere(state);
        }
    }

    /// <summary>
    /// Puts both halves back, with the lock already held.
    /// </summary>
    /// <remarks>
    /// Three writes in a fixed order. The audio half first, so it sounds right. Then the same
    /// bytes to the settings half through SetComponentState, which is how the knobs come to
    /// agree with the sound. Then the settings half's own state, which is the part that says
    /// which preset this is: last, because a plugin works out its display from the sound first
    /// and then puts back whatever it kept for itself on top.
    /// </remarks>
    private void LoadStateHere(byte[] state)
    {
        var (sound, settings) = Apart(state);

        using var stream = new Vst3Stream();
        stream.Fill(sound);

        if (_component->Vtbl->SetState(_component, stream.Pointer) != Vst3Abi.ResultOk) return;

        if (_controller == null) return;

        stream.Rewind();
        _controller->Vtbl->SetComponentState(_controller, stream.Pointer);

        if (settings.Length == 0) return;

        using var mine = new Vst3Stream();
        mine.Fill(settings);

        _controller->Vtbl->SetState(_controller, mine.Pointer);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The view belongs to the settings half, which is the half that knows what the plugin looks
    /// like. A plugin with no settings half has no window either.
    /// </remarks>
    public IPluginEditor? OpenEditor()
    {
        if (_disposed || _controller == null) return null;

        return Vst3Editor.Open(_controller);
    }

    /// <summary>Reads a String128, which is 128 UTF-16 characters with a nought at the end.</summary>
    private static string ReadWide(byte* text)
    {
        if (text == null) return "";

        var wide = (char*)text;

        int length = 0;
        while (length < 128 && wide[length] != '\0') length++;

        return length == 0 ? "" : new string(wide, 0, length);
    }

    /// <summary>
    /// Instances that have been finished with, kept rather than destroyed, ready to be picked
    /// up again by the next slot that wants the same plugin.
    /// </summary>
    /// <remarks>
    /// Same reasoning as <see cref="ClapEffect"/>: a plugin tearing itself down is the code
    /// path least likely to have been exercised by its author, and a fault in there takes the
    /// whole application with it rather than one effect slot.
    /// </remarks>
    private static readonly Dictionary<string, Stack<Vst3Plugin>> Parked = new(StringComparer.Ordinal);

    /// <summary>Held over the parked instances.</summary>
    private static readonly object ParkLock = new();

    /// <summary>
    /// What makes two instances interchangeable: the same class at the same rate and block size.
    /// A parked instance at another rate would have to be set up again, which is the one call
    /// this parking exists to avoid.
    /// </summary>
    private static string Key(string path, string id, int sampleRate, int maxFrames) =>
        path + "|" + id + "|" + sampleRate + "|" + maxFrames;

    /// <summary>
    /// Picks up a parked instance, or null when there is none.
    /// </summary>
    /// <remarks>
    /// Whatever it was holding when it was put down was turned into note offs then, and those
    /// are still in the queue. They go out on the first block, so the plugin does not come back
    /// sounding a chord from its last life; what is cleared here is only the record of which
    /// notes were sounding, since that record is now wrong.
    /// </remarks>
    private static Vst3Plugin? TakeParked(string key)
    {
        Vst3Plugin? effect = null;

        lock (ParkLock)
        {
            if (Parked.TryGetValue(key, out var waiting) && waiting.Count > 0) effect = waiting.Pop();
        }

        if (effect == null) return null;

        effect._disposed = false;

        lock (effect._lock)
        {
            effect._pending.Clear();

            effect._sounding.Clear();
        }

        if (!effect._processing)
        {
            effect._processor->Vtbl->SetProcessing(effect._processor, 1);
            effect._processing = true;
        }

        return effect;
    }

    /// <summary>
    /// Puts the plugin down. It keeps its place in memory, switched off but not taken apart,
    /// ready for the next slot that wants it.
    /// </summary>
    /// <remarks>
    /// Anything still sounding is ended before this is put down. The note offs stay in the queue
    /// rather than being played here: this runs on the UI thread, the audio thread may be in the
    /// middle of a block, and the queue is delivered on the first block after this plugin is
    /// picked up again.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_processing)
        {
            _processor->Vtbl->SetProcessing(_processor, 0);
            _processing = false;
        }

        AllNotesOff();

        lock (_lock) _pending.Clear();

        lock (ParkLock)
        {
            if (!Parked.TryGetValue(_key, out var waiting))
            {
                waiting = new Stack<Vst3Plugin>();
                Parked[_key] = waiting;
            }

            waiting.Push(this);
        }
    }

    /// <summary>
    /// The full teardown, for an instance that never started. Only safe here: a plugin that
    /// failed to activate has nothing to switch off, so the path that faults is not taken.
    /// </summary>
    /// <remarks>
    /// The order matters throughout. Nothing more from its window first: the handler is native
    /// memory the plugin may still be holding, and what it points at has to stop being this
    /// instance before this instance goes anywhere. The two halves are parted before either is
    /// terminated, or each is left holding a pointer to something on its way out. A shared
    /// controller is not released separately, since it is the audio half. And the bundle goes
    /// last: freeing it while the plugin is still in it is a crash with nothing to read in the
    /// stack, because the code the plugin is running has been unmapped.
    /// </remarks>
    private void Retire()
    {
        if (_disposed) return;
        _disposed = true;

        Vst3Host.Forget(_slot);

        if (_processing)
        {
            _processor->Vtbl->SetProcessing(_processor, 0);
            _processing = false;
        }

        if (_active)
        {
            _component->Vtbl->SetActive(_component, 0);
            _active = false;
        }

        if (_controller != null && !_sharedController)
        {
            Part(_component, _controller);

            _controller->Vtbl->SetComponentHandler(_controller, null);
            _controller->Vtbl->Terminate(_controller);
            Release(_controller);
        }

        if (_component != null)
        {
            _component->Vtbl->Terminate(_component);
            Release(_component);
        }

        _changes?.Dispose();
        _changes = null;

        _outgoing?.Dispose();
        _outgoing = null;

        _notes?.Dispose();
        _notes = null;

        _played?.Dispose();
        _played = null;

        Free(_inputData);
        Free(_outputData);
        Free(_inputChannels);
        Free(_outputChannels);
        Free(_inputBusses);
        Free(_outputBusses);
        Free(_process);

        if (_handler != null) NativeMemory.Free(_handler);

        _module.Dispose();
    }

    /// <summary>Frees a block, allowing for one that was never taken.</summary>
    private static void Free(void* memory)
    {
        if (memory != null) NativeMemory.Free(memory);
    }
}
