using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using JingleBox2.ViewModels;
using System;
using JingleBox2.Machines.Interfaces;

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
    /// <remarks>
    /// Trailing underscore because the property that hands it over has the name that belongs to
    /// it, and the interface chose that name first.
    /// </remarks>
    private const double MotionSeconds_ = 0.6;

    /// <summary>Enough of the noise wave to read as noise, and stable between redraws.</summary>
    /// <remarks>
    /// Any number would do. It is a date, so that nobody reading it later goes looking for the
    /// meaning of it.
    /// </remarks>
    private const int NoiseSeed = 20260821;

    /// <summary>How often the noise is reseeded while a note runs, in steps per second.</summary>
    /// <remarks>
    /// The seed moves with the time, so noise drawn a millisecond apart is different noise,
    /// which is what noise should look like. Still, it is drawn from one seed, so a repaint that
    /// asks the same instant twice gets the same picture back rather than a flicker.
    /// </remarks>
    private const double NoiseStepsPerSecond = 1000;

    /// <inheritdoc/>
    public double MotionSeconds => MotionSeconds_;

    /// <inheritdoc/>
    /// <remarks>
    /// The wave travels while a note is sounding, the way a scope shows a running signal, and
    /// stands still otherwise. The pitch it is drawn at is the instrument's tuning plus whatever
    /// the vibrato and the pitch envelope have done to it by that point in the note, which is
    /// why the window is a length of time rather than a count of cycles.
    ///
    /// A fixed seed while it is still, so the noise wave does not crawl on every repaint.
    /// </remarks>
    public void Trace(double[] into, double cycles, double seconds, bool running)
    {
        if (into.Length == 0) return;

        var sound = patch.Patch;
        double makeup = Saturation.Makeup(sound.Drive);

        double semitones = PitchMotion.Tuning(sound)
            + (running ? PitchMotion.MotionAt(sound, seconds) : 0);

        double shown = Math.Max(0.25, cycles) * PitchMotion.Ratio(semitones);

        double travel = running ? seconds * ScrollCyclesPerSecond : 0;

        var noise = new Random(
            running ? NoiseSeed + (int)(seconds * NoiseStepsPerSecond) : NoiseSeed);

        for (int step = 0; step < into.Length; step++)
        {
            double across = into.Length == 1 ? 0 : step / (into.Length - 1.0);
            double phase = Oscillator.Wrap(across * shown + travel);

            double sample = Oscillator.Sample(sound.Wave, phase, sound.Duty, noise.NextDouble() * 2.0 - 1.0);

            into[step] = Saturation.Apply(sample, sound.Drive, makeup);
        }
    }

    /// <inheritdoc/>
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

    /// <summary>Everyone told when the patch moved.</summary>
    private EventHandler? _changed;

    /// <summary>Whether the patch is being watched yet. A latch, never taken off again.</summary>
    private bool _listening;

    /// <summary>Puts the subscription on, once, when somebody first asks to be told.</summary>
    private void Listen()
    {
        if (_listening) return;

        _listening = true;

        patch.PropertyChanged += (_, _) => _changed?.Invoke(this, EventArgs.Empty);
    }
}
