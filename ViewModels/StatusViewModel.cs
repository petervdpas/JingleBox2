using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.UI;
using System;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// What the bar along the bottom shows: where you are, unless something has just happened.
/// </summary>
/// <remarks>
/// The clock only runs while a message is standing in front of the context. Nothing is
/// happening most of the time, and a timer ticking over an unchanging line is a timer running
/// for nothing.
/// </remarks>
public sealed partial class StatusViewModel : ObservableObject
{
    /// <summary>Often enough that a message falls away when it says it will.</summary>
    private const int TickMs = 250;

    /// <summary>Twenty a second: fast enough to look alive, slow enough to cost nothing.</summary>
    private const int MeterMs = 50;

    /// <summary>
    /// How far a meter is allowed to drop in one tick, which is the whole scale in about a
    /// fifth of a second.
    /// </summary>
    private const double FallPerTick = 0.25;

    /// <summary>Where everything in the app says what it has just done, and where you are.</summary>
    private readonly StatusBus _bus;

    /// <summary>
    /// Counts a message down, and runs only while one is standing in front of the context.
    /// </summary>
    private readonly DispatcherTimer _clock;

    /// <summary>
    /// Reads the two levels, and runs whether or not anything is happening.
    /// </summary>
    /// <remarks>
    /// That is the difference between this and <see cref="_clock"/>: a meter that only moved
    /// when something was said would be telling you about the past. It is started only when
    /// somebody handed a meter in, since with neither there is nothing to read.
    /// </remarks>
    private readonly DispatcherTimer _meters;

    /// <summary>What is coming in, asked afresh on every meter tick.</summary>
    private readonly Func<double> _input;

    /// <summary>And what is going out.</summary>
    private readonly Func<double> _output;

    /// <summary>Wires the bar to the bus and, where there is one, to the sound.</summary>
    /// <param name="bus">Where every page says what it has to say, which is what the bar repeats.</param>
    /// <param name="input">What is coming in, 0 to 1.</param>
    /// <param name="output">What is going out, 0 to 1.</param>
    /// <remarks>
    /// Handed in as two functions rather than the audio objects themselves, because a bar along
    /// the bottom of a window has no business knowing what BASS is.
    /// </remarks>
    public StatusViewModel(StatusBus bus, Func<double>? input = null, Func<double>? output = null)
    {
        _bus = bus;
        _input = input ?? (() => 0);
        _output = output ?? (() => 0);

        _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        _clock.Tick += (_, _) => Settle();

        _bus.Posted += (_, _) => Dispatcher.UIThread.Post(Arrived);
        _bus.ContextChanged += (_, _) => Dispatcher.UIThread.Post(Settle);

        _meters = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(MeterMs) };
        _meters.Tick += (_, _) => Read();

        if (input != null || output != null) _meters.Start();

        Settle();
    }

    /// <summary>What is coming in, 0 to 1.</summary>
    [ObservableProperty] private double inputLevel;

    /// <summary>What is going out, 0 to 1.</summary>
    [ObservableProperty] private double outputLevel;

    /// <summary>
    /// Reads both meters. Falling back gently rather than dropping, so a peak can be seen.
    /// </summary>
    /// <remarks>
    /// A meter read twenty times a second and drawn straight would flicker at anything short of
    /// a sustained note, because most of what this app plays is percussive. Rising is instant
    /// and falling takes about a fifth of a second, which is what a meter is expected to do.
    /// </remarks>
    private void Read()
    {
        InputLevel = Fall(InputLevel, Safe(_input));
        OutputLevel = Fall(OutputLevel, Safe(_output));
    }

    /// <summary>
    /// One reading, brought inside 0 to 1, with nought for anything that went wrong.
    /// </summary>
    /// <remarks>
    /// A meter is not worth a crash, and an engine being torn down under it is an ordinary
    /// state rather than a fault: the device is changed from SETTINGS while the bar is still
    /// asking what is playing. A reading that comes back as a NaN is nought for the same
    /// reason, since a NaN put into the bar would stay there.
    /// </remarks>
    private static double Safe(Func<double> read)
    {
        try
        {
            double level = read();

            return double.IsNaN(level) ? 0 : Math.Clamp(level, 0, 1);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>
    /// Where the bar goes next: straight to a louder reading, and down by at most
    /// <see cref="FallPerTick"/>.
    /// </summary>
    private static double Fall(double showing, double read) =>
        read >= showing ? read : Math.Max(read, showing - FallPerTick);

    /// <summary>The line itself.</summary>
    [ObservableProperty] private string text = "";

    /// <summary>Which of the kinds it is, so the bar knows what colour to light.</summary>
    [ObservableProperty] private StatusKind kind = StatusKind.Context;

    /// <summary>Everything said lately, newest first, for looking back at.</summary>
    public string History =>
        string.Join(Environment.NewLine, _bus.Recent.Reverse().Select(m => m.ToString()));

    /// <summary>
    /// Something was said: show it, and start the clock that will take it away again.
    /// </summary>
    /// <remarks>
    /// <see cref="History"/> is told by hand because it is worked out from the bus rather than
    /// held here, so nothing else would ever say it had changed.
    /// </remarks>
    private void Arrived()
    {
        Settle();

        if (!_clock.IsEnabled) _clock.Start();

        OnPropertyChanged(nameof(History));
    }

    /// <summary>
    /// Works out what the bar should say now: the last message while it is still standing, the
    /// context otherwise.
    /// </summary>
    /// <remarks>
    /// The clock is stopped here rather than anywhere else, because this is the one place that
    /// finds out the message has fallen away: back to the context and there is nothing left to
    /// count down.
    /// </remarks>
    private void Settle()
    {
        var last = _bus.Last;
        bool holding = StatusBus.Holding(last, DateTime.Now);

        Text = holding ? last!.Text : _bus.Context;
        Kind = holding ? last!.Kind : StatusKind.Context;

        if (!holding && _clock.IsEnabled) _clock.Stop();
    }
}
