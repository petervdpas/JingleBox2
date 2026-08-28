using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Diagnostics;
using JingleBox2.Midi;
using JingleBox2.Scripting;
using MoonSharp.Interpreter;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Scripting.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Controllers;

namespace JingleBox2.Controllers;

/// <inheritdoc/>
/// <remarks>
/// One <see cref="LuaScript"/> per file, matched to a port by pattern and remembered per device
/// so the list is not walked per message. The folder is watched, so saving a file is all it
/// takes to try the change.
/// </remarks>
public sealed class ControllerCodecs : IControllerCodecs
{
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>Where a controller's own files live, and how one is matched to a port.</summary>
    private readonly IControllerFolder _folder = new ControllerFolder();

    /// <summary>Where a controller's files live. Shared with the profiles beside them.</summary>
    public string Installed => _folder.Installed;

    /// <summary>Where a script's bytes go when it answers a device rather than reading one.</summary>
    private readonly IMidiService _midi;

    /// <summary>Guards the codecs and what has been decided, which are read on the MIDI thread.</summary>
    private readonly object _lock = new();

    /// <summary>Every codec this installation has, in the order the files were read.</summary>
    private readonly List<Codec> _codecs = new();

    /// <summary>What was decided for a device, so the list is not walked per message.</summary>
    private readonly Dictionary<string, Codec?> _decided = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What tells this that a file was saved. Null when the folder cannot be watched.</summary>
    private FileSystemWatcher? _watch;

    /// <summary>Fills the folder if this is a first run, reads what is in it, and watches it.</summary>
    /// <param name="midi">Where a script's bytes go when it answers a device rather than reading one.</param>
    /// <param name="profiles">
    /// What is known about the controllers plugged in. Left out, one of its own; the application
    /// hands the same one to everything, since what a device is doing is remembered in it.
    /// </param>
    public ControllerCodecs(IMidiService midi, IControllerProfiles? profiles = null)
    {
        _profiles = profiles ?? new ControllerProfiles();
        _midi = midi;

        _folder.FirstRun();
        Reload();
        Watch();
    }

    /// <summary>One file, and the device it turned out to be about.</summary>
    private sealed class Codec
    {
        /// <summary>The file, loaded and fenced in.</summary>
        public ILuaScript Script = null!;

        /// <summary>What the script calls itself, or its file name when it does not say.</summary>
        public string Called = "";

        /// <summary>The port names it is for, as a pattern.</summary>
        public string Matches = "";

        /// <summary>The port it is answering for at this moment, for anything it sends back.</summary>
        public string Device = "";
    }

    /// <inheritdoc/>
    public void Reload()
    {
        var found = new List<Codec>();

        try
        {
            if (Directory.Exists(Installed))
                foreach (string file in Directory.GetFiles(Installed, "*.lua").OrderBy(f => f, StringComparer.Ordinal))
                    if (Take(file) is { } one) found.Add(one);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "codecs: cannot read '" + Installed + "': " + bad.Message);
        }

        lock (_lock)
        {
            _codecs.Clear();
            _codecs.AddRange(found);
            _decided.Clear();
        }

        Log.Write(LogArea.Midi, () => "codecs: " + found.Count + " loaded from '" + Installed + "'");
    }

    /// <summary>
    /// Loads one file, gives it what it may reach, and asks it what it is about.
    /// </summary>
    /// <remarks>
    /// Two things are put in the script's reach and no more: <c>log</c>, so somebody writing one
    /// can see what it is doing, and <c>send</c>, so it can answer the device. Bytes go back to
    /// whichever port this codec is answering for at that moment, taken from the codec rather
    /// than from the script, because a script that could name a port could write down somebody
    /// else's.
    ///
    /// What it is about comes from a global table called <c>controller</c>, and a file that sets
    /// none is taken to be for ports named after itself.
    /// </remarks>
    private Codec? Take(string path)
    {
        var script = LuaScript.Open(path);
        if (script is null) return null;

        var codec = new Codec { Script = script, Called = script.Name };

        script.Give("log", (_, args) =>
        {
            string said = args.Count > 0 ? args[0].CastToString() ?? "" : "";
            Log.Write(LogArea.Midi, () => "codec '" + script.Name + "': " + said);

            return DynValue.Nil;
        });

        script.Give("send", (_, args) =>
        {
            if (codec.Device.Length == 0) return DynValue.False;

            var bytes = Bytes(args);
            if (bytes is null || bytes.Length == 0) return DynValue.False;

            return DynValue.NewBoolean(_midi.Send(codec.Device, bytes));
        });

        if (!script.Start()) return null;

        var said = script.Read("controller");
        string called = script.Name;
        string matches = Path.GetFileNameWithoutExtension(path);

        if (said.Type == DataType.Table)
        {
            if (said.Table.Get("name").CastToString() is { Length: > 0 } name) called = name;
            if (said.Table.Get("matches").CastToString() is { Length: > 0 } like) matches = like;
        }

        codec.Called = called;
        codec.Matches = matches;

        Log.Write(LogArea.Midi, () =>
            "codec: '" + script.Name + "' is " + called + ", for ports like '" + matches + "'");

        return codec;
    }

    /// <summary>A list of numbers, or one table of them, as bytes.</summary>
    /// <remarks>
    /// Both spellings, because both are what a person writes without thinking about it, and a
    /// codec refused for calling <c>send</c> the other way is a codec whose author is reading
    /// the log to find out why nothing happened.
    /// </remarks>
    private static byte[]? Bytes(CallbackArguments args)
    {
        if (args.Count == 1 && args[0].Type == DataType.Table)
        {
            var table = args[0].Table;
            var made = new byte[table.Length];

            for (int at = 0; at < made.Length; at++)
                made[at] = (byte)(int)table.Get(at + 1).CastToNumber().GetValueOrDefault();

            return made;
        }

        var one = new byte[args.Count];

        for (int at = 0; at < args.Count; at++)
        {
            if (args[at].CastToNumber() is not { } number) return null;
            one[at] = (byte)(int)number;
        }

        return one;
    }

    /// <inheritdoc/>
    public MidiMessage? Read(MidiMessage message)
    {
        if (message is null) return null;

        var codec = For(message.Device);
        if (codec is null || !codec.Script.Working) return message;

        var told = codec.Script.NewTable();
        var table = told.Table;

        table.Set("device", DynValue.NewString(message.Device));
        table.Set("type", DynValue.NewString(Word(message.Type)));
        table.Set("channel", DynValue.NewNumber(message.Channel));
        table.Set("number", DynValue.NewNumber(message.Value));
        table.Set("value", DynValue.NewNumber(message.Data));
        table.Set("on", DynValue.NewBoolean(message.IsOn));

        codec.Device = message.Device;

        var answer = codec.Script.Call("midi", told);

        if (answer is null || answer.IsNilOrNan()) return message;

        if (answer.Type == DataType.Boolean) return answer.Boolean ? message : null;

        if (answer.Type != DataType.Table) return message;

        return Made(message, answer.Table);
    }

    /// <summary>A message built from what the script handed back, with the rest left alone.</summary>
    /// <remarks>
    /// A kind this does not know leaves the message exactly as it arrived, rather than being
    /// read as some default: a typo in a script should cost the one message it is about.
    ///
    /// A pitch bend is never on, whatever the script said. It is a position and not a press, and
    /// a bend that arrived claiming to be pressed would be read as one by the routers above.
    /// </remarks>
    private static MidiMessage? Made(MidiMessage was, Table table)
    {
        string kind = table.Get("type").CastToString() ?? Word(was.Type);

        var type = kind switch
        {
            "note" => MidiMessageType.Note,
            "cc" => MidiMessageType.ControlChange,
            "bend" => MidiMessageType.PitchBend,
            _ => (MidiMessageType?)null
        };

        if (type is null) return was;

        int channel = Number(table, "channel", was.Channel);
        int number = Number(table, "number", was.Value);
        int value = Number(table, "value", was.Data);

        bool on = table.Get("on").Type == DataType.Boolean
            ? table.Get("on").Boolean
            : value > 0;

        return new MidiMessage
        {
            Device = was.Device,
            Type = type.Value,
            Channel = channel,
            Value = number,
            Data = value,
            IsOn = type == MidiMessageType.PitchBend ? false : on
        };
    }

    /// <summary>A number the script set, or what the message already had.</summary>
    private static int Number(Table table, string key, int otherwise) =>
        table.Get(key).CastToNumber() is { } number ? (int)number : otherwise;

    /// <summary>What a script calls a kind of message, which is a short word rather than a number.</summary>
    /// <remarks>
    /// Only the three a codec can produce have words. Everything else is <c>other</c>, which a
    /// script may read and cannot write, since a codec that could turn a note into a clock byte
    /// would be a codec that could break the transport.
    /// </remarks>
    private static string Word(MidiMessageType type) => type switch
    {
        MidiMessageType.Note => "note",
        MidiMessageType.ControlChange => "cc",
        MidiMessageType.PitchBend => "bend",
        _ => "other"
    };

    /// <summary>The codec for a device, worked out once and remembered.</summary>
    private Codec? For(string device)
    {
        lock (_lock)
        {
            if (_decided.TryGetValue(device, out var known)) return known;

            Codec? found = _codecs.FirstOrDefault(one => _folder.Like(one.Matches, device));
            _decided[device] = found;

            if (found is not null)
                Log.Write(LogArea.Midi, () => "codecs: '" + device + "' is read by " + found.Called);

            return found;
        }
    }

    /// <summary>
    /// Watches the folder, so editing a controller file is editing a file and not a restart.
    /// </summary>
    /// <remarks>
    /// Every file in it, not only the codecs: the profile beside a codec is edited by the same
    /// person in the same sitting, and a folder where one half reloads and the other needs a
    /// restart is a folder nobody can hold in their head. This class owns the only watcher on
    /// that folder, so it tells the profiles as well.
    /// </remarks>
    private void Watch()
    {
        try
        {
            if (!Directory.Exists(Installed)) return;

            _watch = new FileSystemWatcher(Installed)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watch.Changed += OnFolderChanged;
            _watch.Created += OnFolderChanged;
            _watch.Deleted += OnFolderChanged;
            _watch.Renamed += OnFolderChanged;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "codecs: not watching '" + Installed + "': " + bad.Message);
        }
    }

    /// <summary>When the folder was last acted on, for the gathering below.</summary>
    private DateTime _last = DateTime.MinValue;

    /// <summary>How close together two of these count as one save still happening.</summary>
    /// <remarks>
    /// An editor writing a file makes several of them: a create, a write, a rename off a
    /// temporary. Reloading on each would read half-written files and say so in the log.
    /// </remarks>
    private static readonly TimeSpan SameSave = TimeSpan.FromMilliseconds(250);

    /// <summary>Something in the folder was written, so everything in it is read again.</summary>
    private void OnFolderChanged(object? sender, FileSystemEventArgs e)
    {
        var now = DateTime.UtcNow;
        if (now - _last < SameSave) return;
        _last = now;

        try
        {
            Reload();
            _profiles.Reload();
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "controllers: reload failed: " + bad.Message);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Stops the watching. The scripts themselves hold nothing that has to be let go.</remarks>
    public void Dispose()
    {
        _watch?.Dispose();
        _watch = null;
    }
}
