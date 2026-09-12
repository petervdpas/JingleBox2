namespace JingleBox2.Config.Interfaces;

/// <summary>
/// One strip of the mixer desk: what it is set to, and the bus it sets.
/// </summary>
/// <remarks>
/// **One value apiece, written down and applied in the same breath.** A strip used to keep its
/// settings as fields on the engine's bus and nowhere else, so they were this-run-only state with
/// no owner: nothing wrote them and nothing read them back, and the whole desk came up at unity
/// every morning. Writing them into the settings beside the bus would have been two copies of one
/// fact, which is the fault this codebase keeps naming, so it is one: this is where the value
/// lives and the bus is told.
///
/// Nothing here draws anything, so what the desk does can be put a question to without a mixer,
/// an audio engine or a sound card.
/// </remarks>
public interface IDeskStrip
{
    /// <summary>Where the fader stands, in decibels. Nought is unity.</summary>
    double Level { get; set; }

    /// <summary>Where it sits across the stereo field, minus one to one.</summary>
    double Pan { get; set; }

    /// <summary>Whether it is silenced.</summary>
    bool Mute { get; set; }

    /// <summary>Whether it is the only thing being heard.</summary>
    /// <remarks>
    /// **Solo is not a fact about one strip**, it is a statement about every one of them, so what
    /// it comes to is worked out over the whole row and told to the output bus at once. This is
    /// one strip's half of that, which is whether its button is in.
    /// </remarks>
    bool Solo { get; set; }
}
