namespace JingleBox2.Config.Interfaces;

/// <summary>
/// The mixer desk: the four strips that belong to this machine rather than to a song.
/// </summary>
/// <remarks>
/// **A song's mix travels with the song and a desk does not.** The tracker's strips and the song
/// master are in the <c>.jibx</c>, rightly, because they are the music; the recording input, a
/// take being auditioned, the pads together and what leaves the machine are about the room this
/// is being run in. They were in neither place, which is to say they existed only while the
/// application was open.
///
/// **It is a section of the settings rather than a block of its own.** What it holds is written
/// into <see cref="AppConfig"/> and therefore written down by the settings' own writer, on the
/// same clock as everything else: a second block with a second writer would be a second answer to
/// when a file is written, and there is one of those already.
/// </remarks>
public interface IMixerDesk
{
    /// <summary>The recording input's strip.</summary>
    /// <remarks>
    /// Its <see cref="IDeskStrip.Level"/> is the gain on what is coming in rather than a level on
    /// what is going out, which is why the strip says Gain where the others say Level, and why it
    /// is kept in <see cref="AppConfig.RecordGainDb"/> where every settings file already written
    /// has it.
    /// </remarks>
    IDeskStrip In { get; }

    /// <summary>A take being auditioned on RECORD, against the rest of the mix.</summary>
    IDeskStrip Play { get; }

    /// <summary>Every pad, together, against the rest of the mix.</summary>
    IDeskStrip Pads { get; }

    /// <summary>Everything this application is playing, on its way out of the machine.</summary>
    IDeskStrip Master { get; }

    /// <summary>Every strip, for whatever walks them rather than naming one.</summary>
    /// <remarks>Solo is the one that has to: it means only this, over the whole row.</remarks>
    System.Collections.Generic.IReadOnlyList<IDeskStrip> Strips { get; }
}
