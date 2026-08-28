namespace JingleBox2.Audio.Records;

/// <summary>
/// The part of a recording somebody kept, in sample frames.
/// </summary>
/// <remarks>
/// Frames rather than seconds, because this is compared with and turned into positions in the
/// file, and seconds would put a rounding error between the picture and the sound.
/// </remarks>
public class TrimRegion
{
    /// <summary>Where it starts.</summary>
    public long StartSample { get; set; }

    /// <summary>And where it ends. The same as the start means nothing was trimmed.</summary>
    public long EndSample { get; set; }
}
