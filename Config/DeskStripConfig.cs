namespace JingleBox2.Config;

/// <summary>
/// One strip of the mixer desk as the settings file holds it.
/// </summary>
/// <remarks>
/// Plain data, and deliberately so: this is what <c>config.json</c> is made of, and every
/// property here is a name a settings file will use. Renaming one silently drops whatever it
/// held on the next load, so a name here is as good as public.
///
/// **The desk is not the song.** A track's strip belongs to the music and travels in the
/// <c>.jibx</c> with it; these four belong to the machine this is running on, which is why they
/// are here and not there. Set the master fader down because the speakers in this room are loud,
/// and that is true of this room tomorrow and of no song.
/// </remarks>
public sealed class DeskStripConfig
{
    /// <summary>Where the fader stands, in decibels. Nought is unity.</summary>
    public double Level { get; set; }

    /// <summary>Where it sits across the stereo field, minus one to one.</summary>
    public double Pan { get; set; }

    /// <summary>Whether it is silenced.</summary>
    public bool Mute { get; set; }

    /// <summary>Whether it is the only thing being heard.</summary>
    /// <remarks>
    /// Kept like the rest of it, since a solo left on is exactly the thing somebody would go
    /// looking for a fault over: the desk comes back saying only this, and nothing else on it
    /// makes a sound.
    /// </remarks>
    public bool Solo { get; set; }
}
