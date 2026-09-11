using Avalonia.Threading;
using JingleBox2.Audio.Interfaces;
using System;

namespace JingleBox2.Waveform;

/// <summary>
/// Plays a region of a recording and reports where it has got to, as a fraction of the file.
/// Owns the channel and the progress timer so callers never have to.
/// </summary>
/// <remarks>
/// **What it does not own is how a recording is made to sound**, which is
/// <see cref="IRecordingSource"/> and is the same one the pads go through. This opened its own
/// channel and put it on a bus itself, which was a second spelling of an act this application
/// already had, and the two had drifted all one way: the pads' opened the output first and asked
/// for float and prescan, and this did neither.
///
/// **And there is one path out.** A take goes on the take bus the way a pad goes on the pad bus,
/// and where there is no bus there is no sound rather than a second way of playing. The fork this
/// had, playing the channel itself where the bus was not open, is the one the pads were rid of
/// for the reason written on <see cref="IRecordingSource"/>: a channel played that way goes to
/// whatever output the calling thread happens to hold, which behind a driver or a sound server is
/// the device that plays nothing. The take runs, the cursor moves, and nobody hears it.
///
/// What is left here is the region and the clock, and neither is in bytes.
/// </remarks>
public sealed class WaveformPlayer : IDisposable
{
    /// <summary>How a recording is opened, moved about, asked where it is, and let go.</summary>
    private readonly IRecordingSource? _source;

    /// <summary>The bus the take goes onto.</summary>
    private readonly IOutputBus? _bus;

    /// <summary>A player over a source and the bus it lands on.</summary>
    /// <remarks>
    /// **Both are required, so a player that exists can play.** They were optional, which made
    /// the two halves of a wiring mistake and a deliberate silence the same object: a caller that
    /// forgot one got a player that opened nothing and said nothing, and the only way to find out
    /// was that a take made no sound. A window with nothing to play on is a real thing all the
    /// same, and it says so out loud through <see cref="Silent"/>.
    /// </remarks>
    /// <param name="source">How a recording is made to sound.</param>
    /// <param name="bus">Where it goes.</param>
    public WaveformPlayer(IRecordingSource source, IOutputBus bus)
    {
        _source = source;
        _bus = bus;
    }

    /// <summary>Builds one with nowhere to send a take.</summary>
    private WaveformPlayer()
    {
    }

    /// <summary>
    /// A player with nowhere to send a take, which plays nothing whatever it is asked.
    /// </summary>
    /// <remarks>
    /// For a window the toolkit builds for itself, which is handed no audio at all, and for a
    /// page built with none. Named rather than defaulted, so it is a thing somebody asked for
    /// rather than the shape an argument that was left out happens to take.
    /// </remarks>
    public static WaveformPlayer Silent() => new();

    /// <summary>Whether there is anywhere for a take to go.</summary>
    private bool Wired => _source != null && _bus is { IsOpen: true };

    /// <summary>How often the position is read. Ten a second, which a moving line does not need beating.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The channel, or 0 when nothing is playing.</summary>
    private int _channel;

    /// <summary>What reads the position, on the drawing thread. Null when nothing is playing.</summary>
    private DispatcherTimer? _timer;

    /// <summary>Where the region ends, in seconds, or 0 when there is no region.</summary>
    private double _endSeconds;

    /// <summary>How long the recording is, which is what a fraction is a fraction of.</summary>
    private double _seconds;

    /// <summary>Whether a region is playing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Current position as a fraction of the whole file.</summary>
    public event Action<double>? PositionChanged;

    /// <summary>Raised when playback ends, whether it finished or was stopped.</summary>
    public event Action? Stopped;

    /// <summary>Plays from one fraction of the file to another. Both are clamped to 0..1.</summary>
    /// <remarks>
    /// Whatever was playing is stopped first, so a channel or a timer is never left running behind
    /// this one.
    ///
    /// **Every way of not starting leaves <see cref="IsPlaying"/> false**, the bus refusing the
    /// channel included, since a caller lighting a row on the strength of having asked would be
    /// saying something is playing when nothing is and leaving its stop button as the only way out
    /// of a state nobody is in.
    ///
    /// The position is read on a dispatcher timer rather than a pool one, so whoever is listening
    /// may touch controls directly. A pool thread raising these would throw inside Avalonia and
    /// the timer would swallow it.
    /// </remarks>
    /// <param name="filePath">The recording.</param>
    /// <param name="startFraction">Where to start, 0 to 1.</param>
    /// <param name="endFraction">Where to stop, 0 to 1.</param>
    /// <param name="totalFrames">
    /// How long the caller found the file to be, which nought makes this do nothing. It is the
    /// caller's own reading of the file rather than a number this works in: a file that could not
    /// be read is not worth opening twice.
    /// </param>
    public void Play(string filePath, double startFraction, double endFraction, long totalFrames)
    {
        Stop();

        if (totalFrames <= 0 || !Wired) return;

        _channel = _source!.Open(filePath);
        if (_channel == 0) return;

        _seconds = _source.Seconds(_channel);

        if (_seconds <= 0)
        {
            LetGo();

            return;
        }

        double start = Math.Clamp(startFraction, 0, 1) * _seconds;

        _endSeconds = Math.Clamp(endFraction, 0, 1) * _seconds;

        _source.Seek(_channel, start);

        if (!_bus!.Add(_channel))
        {
            LetGo();

            return;
        }

        IsPlaying = true;

        PositionChanged?.Invoke(start / _seconds);

        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    /// <summary>Jumps to a fraction of the file, and does nothing when nothing is playing.</summary>
    /// <param name="fraction">Where to go, 0 to 1.</param>
    public void SeekTo(double fraction)
    {
        if (!IsPlaying || _channel == 0 || _seconds <= 0) return;

        double at = Math.Clamp(fraction, 0, 1);

        _source!.Seek(_channel, at * _seconds);

        PositionChanged?.Invoke(at);
    }

    /// <summary>Stops, lets the channel go, and says so. Does nothing twice.</summary>
    public void Stop()
    {
        _timer?.Stop();
        _timer = null;

        LetGo();

        _endSeconds = 0;
        _seconds = 0;

        if (!IsPlaying) return;

        IsPlaying = false;
        Stopped?.Invoke();
    }

    /// <summary>Takes the channel off the bus and lets it go, and does nothing twice.</summary>
    /// <remarks>
    /// Off the bus first. A source freed while a mixer still holds it is the add-on left pointing
    /// at memory that has gone, and it is the one order here that cannot be got wrong quietly.
    /// </remarks>
    private void LetGo()
    {
        if (_channel == 0) return;

        _bus?.Remove(_channel);
        _source?.Close(_channel);

        _channel = 0;
    }

    /// <summary>
    /// Moves where the region ends, while it is playing.
    /// </summary>
    /// <remarks>
    /// The end is told to this when playing starts, and it used to stay where it was told: in
    /// the trim dialog the handles could be dragged in while a take played and the cursor ran
    /// straight past the selection and on to the end of the file. What is playing is meant to be
    /// the selection, so the selection moving has to reach the thing that is playing it.
    ///
    /// A new end already behind the position stops it, which is what dragging the end back past
    /// what you are hearing means.
    ///
    /// Nothing at all while nothing is playing, since the end is an argument to
    /// <see cref="Play"/> and there is no region to move.
    /// </remarks>
    /// <param name="endFraction">Where the region now ends, 0 to 1.</param>
    public void PlayUntil(double endFraction)
    {
        if (!IsPlaying || _channel == 0 || _seconds <= 0) return;

        _endSeconds = Math.Clamp(endFraction, 0, 1) * _seconds;

        if (_source!.At(_channel) >= _endSeconds) Stop();
    }

    /// <summary>Reads where playback has got to, and stops it at the end of the region.</summary>
    private void Poll()
    {
        if (_channel == 0 || _seconds <= 0)
        {
            Stop();
            return;
        }

        double at = _source!.At(_channel);

        bool reachedEnd = _endSeconds > 0 && at >= _endSeconds;

        if (reachedEnd || _source.Ended(_channel))
        {
            Stop();
            return;
        }

        PositionChanged?.Invoke(at / _seconds);
    }

    /// <summary>Stops whatever is playing.</summary>
    public void Dispose() => Stop();
}
