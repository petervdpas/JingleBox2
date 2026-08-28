namespace JingleBox2.Audio.Records;

/// <summary>
/// How the samples are written in a file, before they are turned into shorts.
/// </summary>
/// <remarks>
/// Reading is generous and writing is not, and this is the shape that difference is expressed
/// in. A sample folder is full of 24-bit and float files saved by editors that default to them,
/// so all of those are read; what the app keeps is one format, because everything downstream,
/// the trim, the normalise and the voices, works in shorts.
/// </remarks>
/// <param name="Format">Which family of layout: whole numbers, floating point, or extensible.</param>
/// <param name="Bits">How wide one sample is, in bits.</param>
public readonly record struct WavStored(int Format, int Bits)
{
    /// <summary>Whole numbers, which is what most files hold.</summary>
    public const int Pcm = 1;

    /// <summary>Floating point, which editors write when asked for the highest quality.</summary>
    public const int Float = 3;

    /// <summary>What a modern editor writes: the real format is inside the sub-format GUID.</summary>
    public const int Extensible = 0xFFFE;

    /// <summary>What this app keeps, and the only width it writes.</summary>
    public const int OurBits = 16;

    /// <summary>How wide one sample is in the file, in bytes.</summary>
    public int Bytes => Bits / 8;

    /// <summary>Whether this is a layout that can be turned into shorts here.</summary>
    public bool Known =>
        (Format == Pcm && Bits is 8 or 16 or 24 or 32) ||
        (Format == Float && Bits is 32 or 64);

    /// <summary>True when the file is already what this app keeps, so a copy is a copy.</summary>
    public bool IsOurs => Format == Pcm && Bits == OurBits;

    /// <summary>How the layout reads in a message somebody is shown.</summary>
    public override string ToString() =>
        Format == Float ? Bits + "-bit float" : Bits + "-bit";
}
