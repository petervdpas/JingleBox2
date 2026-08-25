using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using JingleBox2.ViewModels;
using System;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// The wave a synth patch makes, for a described panel to draw a picture of.
/// </summary>
/// <remarks>
/// Computed the same way the voice computes it, through <see cref="Oscillator"/> and
/// <see cref="Saturation"/>, rather than being an artist's impression of each wave. If the sound
/// changes the picture changes with it, which is the only reason a picture of a wave is worth
/// having at all.
///
/// The window is a length of time and not a number of cycles, so anything that moves the pitch
/// moves the picture: the instrument's tuning always, and while a note runs its vibrato and its
/// pitch envelope as well. That is why the panel hands over where in the note it has got to
/// rather than asking for a still.
/// </remarks>
/// <param name="patch">The patch being drawn.</param>
public sealed class SynthScope(SynthPatchViewModel patch) : IMachineScope
{
    /// <summary>How fast the wave travels while a note sounds, in cycles per second.</summary>
    private const double ScrollCyclesPerSecond = 1.5;

    /// <summary>How long it keeps moving after a note, since a preview is about that long.</summary>
    private const double MotionSeconds_ = 0.6;

    /// <summary>Enough of the noise wave to read as noise, and stable between redraws.</summary>
    private const int NoiseSeed = 20260821;

    public double MotionSeconds => MotionSeconds_;

    public void Trace(double[] into, double cycles, double seconds, bool running)
    {
        if (into.Length == 0) return;

        var sound = patch.Patch;
        double makeup = Saturation.Makeup(sound.Drive);

        double semitones = PitchMotion.Tuning(sound)
            + (running ? PitchMotion.MotionAt(sound, seconds) : 0);

        double shown = Math.Max(0.25, cycles) * PitchMotion.Ratio(semitones);

        // The wave travels while a note is sounding, the way a scope shows a running signal.
        double travel = running ? seconds * ScrollCyclesPerSecond : 0;

        // A fixed seed while it is still, so the noise wave does not crawl on every repaint;
        // while it runs, a new one per frame is exactly what noise should look like.
        var noise = new Random(running ? NoiseSeed + (int)(seconds * 1000) : NoiseSeed);

        for (int step = 0; step < into.Length; step++)
        {
            double across = into.Length == 1 ? 0 : step / (into.Length - 1.0);
            double phase = Oscillator.Wrap(across * shown + travel);

            double sample = Oscillator.Sample(sound.Wave, phase, sound.Duty, noise.NextDouble() * 2.0 - 1.0);

            into[step] = Saturation.Apply(sample, sound.Drive, makeup);
        }
    }

    /// <summary>
    /// Told when the patch moved, so the picture is drawn again.
    /// </summary>
    /// <remarks>
    /// The patch is plain data and says nothing when it is edited; its view model is what counts
    /// the changes, and that is what this listens to.
    /// </remarks>
    public event EventHandler? Changed
    {
        add
        {
            _changed += value;

            Listen();
        }
        remove => _changed -= value;
    }

    private EventHandler? _changed;

    private bool _listening;

    private void Listen()
    {
        if (_listening) return;

        _listening = true;

        patch.PropertyChanged += (_, _) => _changed?.Invoke(this, EventArgs.Empty);
    }
}
