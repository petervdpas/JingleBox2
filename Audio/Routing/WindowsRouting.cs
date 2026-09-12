using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing;

/// <summary>
/// Choosing what to record on Windows: the capture devices, and what each output is playing.
/// </summary>
/// <remarks>
/// Windows has no graph to patch, so nothing is being rewired here. The list offers what the
/// system can capture, and picking one points the recorder at it: a device through BASS as
/// before, an output through WASAPI loopback, or one program on its own through per-process
/// loopback, which is what makes this page mean the same thing on both machines.
///
/// Setting the recorder's loopback device reopens the capture, so a route picked here is heard
/// straight away rather than the next time the input happens to be opened.
/// </remarks>
public sealed class WindowsRouting : IAudioRouting
{
    /// <summary>
    /// In front of an output's number, for a route that records what that output is playing.
    /// </summary>
    /// <remarks>
    /// Declared rather than composed from a variable, so both kinds of id stay greppable: a
    /// node string is written in one place and read in another, and an id built out of pieces
    /// is one nobody can search for.
    /// </remarks>
    private const string LoopbackPrefix = "loopback:";

    /// <summary>In front of a capture device's name, for a route that records that device.</summary>
    private const string DevicePrefix = "device:";

    /// <summary>In front of a program's process id, for a route that records that program.</summary>
    /// <inheritdoc cref="LoopbackPrefix" path="/remarks"/>
    private const string ProgramPrefix = "program:";

    /// <summary>The recorder, which is what is actually pointed somewhere: nothing is rewired here.</summary>
    private readonly IRecordingService _recording;

    /// <summary>Takes the recorder this will be pointing at devices, outputs and programs.</summary>
    /// <param name="recording">What is actually pointed somewhere.</param>
    public WindowsRouting(IRecordingService recording) => _recording = recording;

    /// <summary>What the last look found, which is what <see cref="IsAvailable"/> answers.</summary>
    /// <remarks>
    /// Written by <see cref="GetRoutes"/>, which runs on the pool, and read by the drawing
    /// thread, so the pair is ordered rather than merely present: the answer is written first
    /// and <see cref="_looked"/> after it, and both are volatile, so nobody can see that a look
    /// has happened and read the answer from before it. Two threads looking at once is harmless,
    /// since they are asking the machine the same question and it has one answer.
    /// </remarks>
    private volatile bool _can;

    /// <summary>Whether anything has looked yet.</summary>
    /// <inheritdoc cref="_can" path="/remarks"/>
    private volatile bool _looked;

    /// <inheritdoc/>
    /// <remarks>
    /// Windows, and the system really offering something this page can reach: a loopback output
    /// or a program playing on its own. With neither, this offers nothing the recorder's own
    /// device picker does not already, so it stands down rather than showing the same devices
    /// twice.
    /// **What that costs is the whole reason this is not worked out per ask.** Both halves of it
    /// are a walk of the machine's audio endpoints through COM, which is hundreds of milliseconds
    /// on an ordinary box, and a property is read from a binding and from the head of a tick on
    /// the drawing thread. Asked there twice a second it is the drawing thread gone for a third
    /// of a second at a time, which is not audible and is entirely visible: the tracker's picture
    /// stops while the transport does not, so the pattern arrives in clumps of three or four
    /// lines. Measured on a machine it was reported on as steps landing 0.1 ms apart and then
    /// not for 455 ms, against a mean of exactly 125.0.
    ///
    /// So it looks once, where the first ask happens to be, and keeps what it found;
    /// <see cref="GetRoutes"/> settles it again every time it reads, which is off the drawing
    /// thread and is already walking both lists. There is no clock in it and nothing to keep in
    /// step: the answer is a by-product of the reading that was going to happen anyway.
    /// </remarks>
    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;
            if (_looked) return _can;

            return Looked(Look());
        }
    }

    /// <summary>Asks the machine whether there is anything here to offer.</summary>
    /// <remarks>
    /// The slow one, and the only place it is worked out. Anything thrown is no rather than a
    /// fault, since a page that cannot read the machine has nothing to show either way.
    /// </remarks>
    private bool Look()
    {
        try
        {
            return _recording.GetLoopbackDevices().Count > 0 || _recording.GetPrograms().Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Writes down what a look found, and hands it straight back.</summary>
    /// <inheritdoc cref="_can" path="/remarks"/>
    /// <param name="can">What was found.</param>
    /// <returns>The same answer, so a caller can settle and answer in one line.</returns>
    private bool Looked(bool can)
    {
        _can = can;
        _looked = true;

        return can;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The capture devices first, then the outputs, then the programs, which is the same reading
    /// order the PipeWire side produces.
    ///
    /// **A program on its own is in the list now**, which is what makes this page mean the same
    /// thing on both machines: what a PipeWire node gives for nothing, Windows answers with
    /// per-process loopback. It copies rather than moves, so the program is still heard wherever
    /// it was playing.
    ///
    /// Anything that goes wrong reading the two lists comes back as no routes at all rather
    /// than half of them, since half a list is a page that looks complete and is not.
    ///
    /// **This is also where <see cref="IsAvailable"/> is settled**, out of the two lists it was
    /// going to walk anyway, so the expensive question is asked once per reading and on a thread
    /// that may take as long as it likes. Both lists are taken before anything is offered, since
    /// with neither there is nothing here the recorder's own device picker does not already show
    /// and the capture devices are left out with the rest.
    /// </remarks>
    public IReadOnlyList<AudioRoute> GetRoutes()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<AudioRoute>();

        var routes = new List<AudioRoute>();

        try
        {
            var outputs = _recording.GetLoopbackDevices();
            var programs = _recording.GetPrograms();

            if (!Looked(outputs.Count > 0 || programs.Count > 0)) return Array.Empty<AudioRoute>();

            foreach (var device in _recording.GetInputDevices())
                routes.Add(new AudioRoute(DevicePrefix + device, device, AudioRouteKind.Input));

            foreach (var output in outputs)
            {
                routes.Add(new AudioRoute(
                    LoopbackPrefix + output.Index.ToString(CultureInfo.InvariantCulture),
                    output.Name,
                    AudioRouteKind.Monitor));
            }

            foreach (var program in programs)
            {
                routes.Add(new AudioRoute(
                    ProgramPrefix + program.ProcessId.ToString(CultureInfo.InvariantCulture),
                    program.Name,
                    AudioRouteKind.Application));
            }
        }
        catch (Exception)
        {
            return Array.Empty<AudioRoute>();
        }

        return routes;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Asked of the recorder rather than remembered: a loopback output wins where one is set,
    /// and the selected capture device answers otherwise. The answer is matched back against
    /// the offered list, so the page marks the row it is already showing rather than a second
    /// route that merely says the same thing.
    /// </remarks>
    public AudioRoute? GetCurrentRoute()
    {
        if (!IsAvailable) return null;

        if (_recording.LoopbackProgram is int program)
        {
            string node = ProgramPrefix + program.ToString(CultureInfo.InvariantCulture);

            return GetRoutes().FirstOrDefault(r => r.Node == node);
        }

        var routes = GetRoutes();

        if (_recording.LoopbackDevice is int loopback)
        {
            string node = LoopbackPrefix + loopback.ToString(CultureInfo.InvariantCulture);
            return routes.FirstOrDefault(r => r.Node == node);
        }

        string? device = _recording.SelectedDevice;
        if (string.IsNullOrEmpty(device)) return null;

        return routes.FirstOrDefault(r => r.Node == DevicePrefix + device);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing is rewired: the prefix says which of the recorder's three ways of listening is
    /// meant, and the recorder is set accordingly. Each of them clears the other two first, or
    /// the recorder would go on taking whatever it was given before: a program wins over an
    /// output, so a stale program id would quietly outlive the choice that replaced it.
    /// </remarks>
    public bool Connect(AudioRoute route)
    {
        if (!IsAvailable || route == null) return false;

        try
        {
            if (route.Node.StartsWith(ProgramPrefix, StringComparison.Ordinal))
            {
                string id = route.Node[ProgramPrefix.Length..];
                if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int program))
                    return false;

                _recording.LoopbackDevice = null;
                _recording.LoopbackProgram = program;

                return true;
            }

            if (route.Node.StartsWith(LoopbackPrefix, StringComparison.Ordinal))
            {
                string index = route.Node[LoopbackPrefix.Length..];
                if (!int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int device))
                    return false;

                _recording.LoopbackProgram = null;
                _recording.LoopbackDevice = device;

                return true;
            }

            if (!route.Node.StartsWith(DevicePrefix, StringComparison.Ordinal)) return false;

            _recording.LoopbackProgram = null;
            _recording.LoopbackDevice = null;
            _recording.SelectedDevice = route.Node[DevicePrefix.Length..];
            _recording.ReopenInput();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Three things at once: the machine can be told where a program plays, somebody has chosen
    /// where an unheard source should go, and that output is still on the machine. The last one
    /// is why the list is read rather than trusted: a cable uninstalled or a socket unplugged
    /// since the choice was made would send a programme to an id that no longer names anything.
    /// </remarks>
    public bool CanTakeAside => false;

    /// <inheritdoc/>
    public string AsideNote => "this build cannot tell Windows where a program plays";

    /// <inheritdoc/>
    /// <remarks>
    /// **Nothing can be taken aside on Windows in this build**, and it is refused rather than
    /// half done. Taking a source aside here would mean telling the system where a program plays,
    /// which is <c>IAudioPolicyConfig</c>: an interface that is on the machine, is not documented,
    /// and will not activate from this runtime. What stood on top of it was a picker asking where
    /// a source should be sent, a stored choice, and a call that never came back, which is three
    /// moving parts around a thing that has never once worked.
    ///
    /// A source can still be recorded here. What it cannot be is silenced where it was playing,
    /// so it is heard twice, and <see cref="AsideNote"/> is the sentence that says so.
    /// </remarks>
    public bool TakeAside(AudioRoute route) => false;

    /// <inheritdoc/>
    /// <remarks>
    /// **The system holds this one, so there is nothing to hold here.** What is set on a graph is
    /// a wire, which the session manager remakes the moment a stream is; what is set here is a
    /// standing instruction about where a program plays, which its next stream follows without
    /// anybody asking again. So the answer is false rather than nought work done: nothing had
    /// crept back, because nothing can.
    ///
    /// The one thing there is to do is the case where the arrangement was never made, which is a
    /// source the picker chose on somebody's behalf: that went through no choice, so a switch
    /// already on has nothing standing behind it.
    /// </remarks>
    public bool HoldAside(AudioRoute route) => false;

    /// <inheritdoc/>
    /// <remarks>
    /// The program is forgotten whatever the call answered, since a program that has since ended
    /// cannot be given anything back and trying again on every source change would be this
    /// application arguing with the system for the rest of the session.
    /// </remarks>
    public void GiveBack()
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Answered by the name, and here that is sound rather than a hopeful guess: a monitor route
    /// is built from <c>GetLoopbackDevices</c> and the output picker is built from the same
    /// endpoints, so both halves are the system's own word for the same device. The indices are
    /// not comparable and the names are, which is why it is the name that is read.
    ///
    /// Trimmed and without regard to case, like every other name compared here. With no output
    /// chosen there is nothing to compare it against, which is the cannot-tell answer rather than
    /// a no.
    /// </remarks>
    public bool? IsOurOutput(AudioRoute source, string? output)
    {
        if (source is null) return null;
        if (string.IsNullOrWhiteSpace(output)) return null;

        return string.Equals(source.Name.Trim(), output.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
