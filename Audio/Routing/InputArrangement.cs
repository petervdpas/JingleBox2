using System;
using System.Diagnostics;
using System.Timers;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Audio.Routing;

/// <inheritdoc/>
public sealed class InputArrangement : IInputArrangement
{
    /// <summary>How often the arrangement is looked at.</summary>
    /// <remarks>
    /// The couple of seconds the page's own reading used, which is what the arrangement was held
    /// at before this existed: fast enough that a source creeping back is a blip rather than a
    /// programme coming on, and slow enough to be nothing on the clock.
    /// </remarks>
    private static readonly TimeSpan Often = TimeSpan.FromSeconds(2);

    /// <summary>How often it is looked at while the graph is still moving under a change.</summary>
    /// <remarks>
    /// **One look is not enough, because the same gesture that makes the arrangement also moves
    /// the graph it is made on.** Choosing a source opens this application's capture, which
    /// appears in the graph a moment after it is asked for; letting one go puts the source's own
    /// links back, which the session manager takes a moment to do. So the look taken at the
    /// instant of the gesture is a look at a graph that has not finished changing, and the
    /// ordinary clock is two seconds away: two seconds of a source playing in two places.
    ///
    /// A fifth of a second is under what a hand notices. Nothing here is a retry of a failure:
    /// each look takes off whatever has come back since the last one, so a graph that settles at
    /// once is a few looks that find nothing.
    /// </remarks>
    private static readonly TimeSpan Quickly = TimeSpan.FromMilliseconds(200);

    /// <summary>How long the quick looks go on after a change before the slow clock takes over.</summary>
    /// <remarks>
    /// Long enough for a session manager to have finished whatever it was doing, and short enough
    /// that a machine where this never settles is not running graph tools at that rate for the
    /// rest of the session.
    /// </remarks>
    private static readonly TimeSpan Settles = TimeSpan.FromSeconds(4);

    /// <summary>What the pages have asked for.</summary>
    private readonly IInputSetting _setting;

    /// <summary>What moves the links about and knows what it has moved.</summary>
    private readonly IInputPath _input;

    /// <summary>How a piece of work is got off the thread that asked for it.</summary>
    /// <remarks>
    /// Handed in for the reason <c>ControlWrites</c>'s trip to the drawing thread is handed in:
    /// the application has a thread pool and a test wants the work where it can see it, and the
    /// whole of what is worth checking here is what the arrangement came to rather than which
    /// thread it came to it on.
    /// </remarks>
    private readonly Action<Action> _away;

    /// <summary>The clock, which runs for as long as this does.</summary>
    /// <remarks>
    /// Started once and never stopped short of the way out, rather than turned on and off with
    /// the arrangement: a clock that has to be switched on at the right moment is one more thing
    /// that can be missed, and being missed is the whole of what this type was written for. A
    /// tick with nothing aside is a comparison.
    ///
    /// Not the drawing thread's clock. Holding an arrangement reads and rewires the machine's
    /// audio graph, which is another program's and takes a moment.
    /// </remarks>
    private readonly Timer _clock;

    /// <summary>Whether a look is already going on, since a graph read can outlast a tick.</summary>
    private bool _looking;

    /// <summary>How long since the setting moved, which is what the quick looks are for.</summary>
    private readonly Stopwatch _since = Stopwatch.StartNew();

    /// <summary>And how long since the last look, so the clock's rate is one comparison.</summary>
    private readonly Stopwatch _looked = Stopwatch.StartNew();

    /// <inheritdoc/>
    public InputArranged Aside { get; private set; }

    /// <inheritdoc/>
    public event Action<InputArranged>? Arranged;

    /// <inheritdoc/>
    public event Action<AudioRoute>? PutBack;

    /// <summary>Follows a setting, and makes the machine match it from now on.</summary>
    /// <param name="setting">What the pages have asked for.</param>
    /// <param name="input">What moves the links about.</param>
    /// <param name="away">
    /// How to get off the thread that wrote the setting. The thread pool unless somebody says
    /// otherwise, which is what the application wants; a caller that hands in one running the
    /// work where it stands gets the arrangement made before the call to say so comes back.
    /// </param>
    public InputArrangement(IInputSetting setting, IInputPath input, Action<Action>? away = null)
    {
        _setting = setting;
        _input = input;
        _away = away ?? (work => System.Threading.Tasks.Task.Run(work));

        _setting.Changed += Follow;

        _clock = new Timer(Quickly.TotalMilliseconds) { AutoReset = true };
        _clock.Elapsed += (_, _) => Tick();
        _clock.Start();
    }

    /// <summary>
    /// The setting moved, so the machine is made to match it.
    /// </summary>
    /// <remarks>
    /// **Off the thread that said so**, because making the machine match runs the graph's own
    /// command line tools and takes a moment: the page that wrote the setting is the drawing
    /// thread, and half a second of frozen window is not what a dropdown should cost. What came
    /// of it is said afterwards through <see cref="Arranged"/>.
    /// </remarks>
    private void Follow() => _away(Again);

    /// <inheritdoc/>
    public void Again()
    {
        _since.Restart();

        try
        {
            Aside = _input.Set(_setting.Source, _setting.Heard, _setting.PlayingOut);

            Arranged?.Invoke(Aside);
        }
        catch (Exception bad)
        {
            Log.Fault(LogArea.Audio, "the input could not be arranged", bad);
        }
    }

    /// <summary>
    /// The clock, which looks quickly while the graph is settling and slowly the rest of the time.
    /// </summary>
    /// <remarks>
    /// One timer at the quick rate rather than two clocks or a rate that is changed, since a rate
    /// that is changed is a thing that can be left at the wrong one. A tick that is not due is a
    /// comparison.
    /// </remarks>
    private void Tick()
    {
        if (_looked.Elapsed < (_since.Elapsed < Settles ? Quickly : Often)) return;

        _looked.Restart();

        Check();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Anything thrown is written down and nothing more. The graph is another program's and can
    /// answer badly at any moment; what may not happen is this taking the application with it
    /// from a thread nobody is watching.
    /// </remarks>
    public void Check()
    {
        if (_looking || !_input.Aside) return;

        try
        {
            _looking = true;

            if (!_input.Hold()) return;

            var source = _setting.Source;

            Log.Write(LogArea.Audio, () =>
                "routing: " + (source?.Display ?? "the source")
                + " had got back onto its own output and was taken off again");

            if (source != null) PutBack?.Invoke(source);
        }
        catch (Exception bad)
        {
            Log.Fault(LogArea.Audio, "the arrangement could not be held", bad);
        }
        finally
        {
            _looking = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _setting.Changed -= Follow;

        _clock.Stop();
        _clock.Dispose();
    }
}
