using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Tracker.Effects.Interfaces;

/// <summary>
/// One of our effects, running: the audio it works on and the knobs it is set by.
/// </summary>
/// <remarks>
/// An <see cref="IAudioInsert"/> is only the audio. What an effect of ours needs on top of that
/// is the pair of questions everything else in this application asks of a thing with settings:
/// what is this parameter standing at, and put it there. The panel reads it, the chain writes it
/// down, a knob on the desk moves it and a lane over the pattern moves it, and all four are the
/// same two calls.
///
/// Keyed by the effect's own words, the same strings its manifest names, because that is what
/// travels: a machine's parameter is named by its key rather than by a number, and an effect is
/// no different. A key this effect does not have reads as nought and writes nothing, so a chain
/// saved by a later version is read as far as it goes rather than refused.
///
/// Both halves are called from two threads. A value is written by a hand on the drawing thread
/// or by the MIDI thread and read on the audio thread on every block, so what is stored is a
/// single word: the worst that can happen is one block working from the value before last.
/// </remarks>
public interface IEffectEngine : IAudioInsert
{
    /// <summary>Where that parameter is standing, or nought for one this effect has not got.</summary>
    /// <param name="key">The parameter's own word, as its manifest names it.</param>
    double ValueOf(string? key);

    /// <summary>
    /// Puts that parameter there, within whatever range it has.
    /// </summary>
    /// <remarks>
    /// Held to the parameter's own ends rather than trusted: what arrives here comes off a file,
    /// a controller or a lane, and a delay time of a million seconds is a buffer nobody has.
    /// Anything that is not a number at all is refused outright, since <c>Math.Clamp</c> hands
    /// NaN back and one NaN in a feedback loop is silence for the rest of the session.
    /// </remarks>
    /// <param name="key">The parameter's own word.</param>
    /// <param name="value">Where to put it.</param>
    void SetValue(string? key, double value);
}
