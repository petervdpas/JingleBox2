namespace JingleBox2.Tracker.Enums;

/// <summary>
/// Which machine an instrument is on, as the number every song and instrument file already
/// holds.
/// </summary>
/// <remarks>
/// These numbers are in people's files, so they do not move and none is ever reused. The
/// readable side of the same fact, what the machine is called and what it is for, is
/// <see cref="Machine"/>: an instrument of a kind whose machine is not installed here still
/// sounds and still has to be named, and what it is named is the engine rather than a machine
/// that is not there.
/// </remarks>
public enum TrackerInstrumentKind
{
    /// <summary>One of your recordings, pitched by resampling.</summary>
    Sample = 0,

    /// <summary>Generated on the fly from a patch, so it needs no file at all.</summary>
    Synth = 1,

    /// <summary>A plugin doing the playing: Serum, Vital, anything that takes notes.</summary>
    Plugin = 2,

    /// <summary>Ouroboros: one oscillator, a filter that sweeps, and glide between notes.</summary>
    MonoSynth = 3,

    /// <summary>BongaBong: a kit, one recording to a key, none of them transposed.</summary>
    Kit = 4,

    /// <summary>Zampler: recordings laid across the keyboard, each transposed from its root.</summary>
    Sampler = 5
}
