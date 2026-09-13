namespace JingleBox2.SoundDevices.SoundEffects.Interfaces;

/// <summary>
/// A slow sine an effect moves one of its own knobs with: a phaser's sweep, a filter going wah
/// on its own, a ring modulator's carrier drifting up and down.
/// </summary>
/// <remarks>
/// Written once because four effects want one and a phase kept four ways is four ways of getting
/// the wrap wrong. It holds where it has got to and nothing else, so the rate is handed in on
/// every step rather than kept: a knob moved on the drawing thread lands on the next sample
/// without a second copy of it to go stale.
///
/// **The phase is continuous whatever the rate does.** The step is added to where it was, so
/// turning the rate knob changes how fast it goes from here rather than where it is, which is
/// the difference between a sweep speeding up and a sweep jumping.
///
/// Stereo is one oscillator read twice, the second <see cref="Beside"/> the first by a fraction
/// of a turn, since two oscillators started together drift apart by rounding and a spread that
/// wanders is not a spread.
///
/// Lives on the audio thread and nothing in it allocates, takes a lock or blocks.
/// </remarks>
public interface ISlowOscillator
{
    /// <summary>How far round it is, from nought to one.</summary>
    double Phase { get; }

    /// <summary>Moves one sample on and answers where the sine stands, minus one to one.</summary>
    /// <param name="hertz">How many turns a second, read now; nothing that is not a number moves it.</param>
    double Step(double hertz);

    /// <summary>Where the sine stands that fraction of a turn ahead of where it is now.</summary>
    /// <param name="turn">How far ahead, from nought to one; a half is the other side.</param>
    double Beside(double turn);
}
