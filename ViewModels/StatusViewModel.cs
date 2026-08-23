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

    private readonly StatusBus _bus;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _meters;

    private readonly Func<double> _input;
    private readonly Func<double> _output;

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

        // The meters run whether or not anything is happening, which is the difference between
        // them and the message clock: a meter that only moved when something was said would be
        // telling you about the past.
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

    private static double Safe(Func<double> read)
    {
        try
        {
            double level = read();

            return double.IsNaN(level) ? 0 : Math.Clamp(level, 0, 1);
        }
        catch (Exception)
        {
            // A meter is not worth a crash, and an engine being torn down under it is normal.
            return 0;
        }
    }

    private static double Fall(double showing, double read) =>
        read >= showing ? read : Math.Max(read, showing - 0.25);

    /// <summary>The line itself.</summary>
    [ObservableProperty] private string text = "";

    /// <summary>Which of the kinds it is, so the bar knows what colour to light.</summary>
    [ObservableProperty] private StatusKind kind = StatusKind.Context;

    /// <summary>Everything said lately, newest first, for looking back at.</summary>
    public string History =>
        string.Join(Environment.NewLine, _bus.Recent.Reverse().Select(m => m.ToString()));

    private void Arrived()
    {
        Settle();

        if (!_clock.IsEnabled) _clock.Start();

        OnPropertyChanged(nameof(History));
    }

    private void Settle()
    {
        var last = _bus.Last;
        bool holding = StatusBus.Holding(last, DateTime.Now);

        Text = holding ? last!.Text : _bus.Context;
        Kind = holding ? last!.Kind : StatusKind.Context;

        // Back to the context and nothing left to count down.
        if (!holding && _clock.IsEnabled) _clock.Stop();
    }
}
