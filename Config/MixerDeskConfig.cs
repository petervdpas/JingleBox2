namespace JingleBox2.Config;

/// <summary>
/// The mixer desk as the settings file holds it: the four strips that are not a song's.
/// </summary>
/// <remarks>
/// **The desk is what this machine is set up like, and the song is the music.** The tracker's
/// strips and the song master live in the <c>.jibx</c> and travel with the piece; the recording
/// input, a take being auditioned, the pads together and what leaves the machine are about the
/// room this is being run in, and they were written down nowhere at all: set the master fader and
/// restart, and it was back at unity with nothing saying why.
///
/// Absent in every settings file written before this, which reads back as a desk at unity with
/// nothing panned, muted or soloed, and that is exactly what those installations had.
/// </remarks>
public sealed class MixerDeskConfig
{
    /// <summary>The recording input's strip.</summary>
    /// <remarks>
    /// Its fader is not in here and is <see cref="AppConfig.RecordGainDb"/>, which is where every
    /// settings file already written keeps it. That is not only compatibility: what that fader
    /// moves is the gain on what is coming in, before anything is written, so it decides what a
    /// take holds rather than what the desk sends out. The rest of the strip is the desk's like
    /// any other.
    /// </remarks>
    public DeskStripConfig In { get; set; } = new();

    /// <summary>A take being auditioned on RECORD, against the rest of the mix.</summary>
    public DeskStripConfig Play { get; set; } = new();

    /// <summary>Every pad, together, against the rest of the mix.</summary>
    public DeskStripConfig Pads { get; set; } = new();

    /// <summary>Everything this application is playing, on its way out of the machine.</summary>
    public DeskStripConfig Master { get; set; } = new();
}
