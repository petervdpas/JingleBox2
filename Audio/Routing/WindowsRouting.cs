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
/// before, or an output through WASAPI loopback. One program on its own is not in reach this
/// way; that needs per-process loopback, which is a different piece of work.
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

    /// <summary>What tells the system where a program plays, for taking a source aside.</summary>
    private readonly IProgramOutput _output;

    /// <summary>Where a source is sent so nobody hears it, or nothing while none is chosen.</summary>
    private readonly ISilentOutput? _silent;

    /// <summary>Which program was sent there, so it can be given its own choice back.</summary>
    private int? _aside;

    /// <summary>Takes the recorder this will be pointing at devices, outputs and programs.</summary>
    /// <param name="recording">What is actually pointed somewhere.</param>
    /// <param name="silent">Where a source goes to be unheard, or nothing where nobody chose.</param>
    /// <param name="output">
    /// What tells the system where a program plays. Defaulted to the machine's own, so a caller
    /// who does not care pays nothing and a test can hand one in.
    /// </param>
    public WindowsRouting(
        IRecordingService recording,
        ISilentOutput? silent = null,
        IProgramOutput? output = null)
    {
        _recording = recording;
        _silent = silent;
        _output = output ?? (OperatingSystem.IsWindows() ? new WindowsProgramOutput() : new NoProgramOutput());
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Windows, and the system really offering loopback. No loopback devices means the add-on
    /// is missing or the system will not do it, and then this offers nothing the recorder's own
    /// device picker does not already, so it stands down rather than showing the same devices
    /// twice.
    /// </remarks>
    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;

            try
            {
                return _recording.GetLoopbackDevices().Count > 0 || _recording.GetPrograms().Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
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
    /// </remarks>
    public IReadOnlyList<AudioRoute> GetRoutes()
    {
        if (!IsAvailable) return Array.Empty<AudioRoute>();

        var routes = new List<AudioRoute>();

        try
        {
            foreach (var device in _recording.GetInputDevices())
                routes.Add(new AudioRoute(DevicePrefix + device, device, AudioRouteKind.Input));

            foreach (var output in _recording.GetLoopbackDevices())
            {
                routes.Add(new AudioRoute(
                    LoopbackPrefix + output.Index.ToString(CultureInfo.InvariantCulture),
                    output.Name,
                    AudioRouteKind.Monitor));
            }

            foreach (var program in _recording.GetPrograms())
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
    public bool CanTakeAside => _output.CanPoint && Silent() != null;

    /// <inheritdoc/>
    /// <remarks>
    /// The two halves of <see cref="CanTakeAside"/> said apart, because only one of them is
    /// anybody's to fix. No output chosen is a picker two lines above the switch; no way to point
    /// a program is this installation, and telling somebody to choose an output when the answer
    /// would not be taken either is sending them somewhere pointless.
    ///
    /// The order is the order they can be acted on: what somebody can do first is said first.
    /// </remarks>
    public string AsideNote =>
        !_output.CanPoint
            ? "this build cannot tell Windows where a program plays"
            : Silent() == null
                ? "nothing is chosen under Send it to, and a source here is moved to another "
                  + "output rather than unplugged, so there has to be one to move it to"
                : "";

    /// <inheritdoc/>
    /// <remarks>
    /// **Only a program can be taken aside here.** A capture device is not something that plays,
    /// and an output's own playback is the whole of what a machine is doing rather than one
    /// program's share of it: taking either aside means nothing, so it is refused rather than
    /// half done.
    /// </remarks>
    public bool TakeAside(AudioRoute route)
    {
        if (route == null || Silent() is not { } silent) return false;
        if (!route.Node.StartsWith(ProgramPrefix, StringComparison.Ordinal)) return false;

        string id = route.Node[ProgramPrefix.Length..];

        if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int program)) return false;

        GiveBack();

        if (!_output.Point(program, silent)) return false;

        _aside = program;

        return true;
    }

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
    public bool HoldAside(AudioRoute route)
    {
        if (route == null || _aside != null) return false;

        return TakeAside(route);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The program is forgotten whatever the call answered, since a program that has since ended
    /// cannot be given anything back and trying again on every source change would be this
    /// application arguing with the system for the rest of the session.
    /// </remarks>
    public void GiveBack()
    {
        if (_aside is not int program) return;

        _aside = null;

        _output.Release(program);
    }

    /// <summary>The chosen output, where it is chosen and still on the machine.</summary>
    private string? Silent()
    {
        if (_silent?.Chosen is not { } chosen || chosen.Length == 0) return null;

        foreach (var output in _silent.Outputs)
            if (string.Equals(output.Id, chosen, StringComparison.Ordinal)) return chosen;

        return null;
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
