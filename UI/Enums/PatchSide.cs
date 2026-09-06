namespace JingleBox2.UI.Enums;

/// <summary>Which side of a block a connection point is on.</summary>
/// <remarks>
/// Audio arrives on the left and leaves on the right, which is the way every patchbay ever
/// drawn reads and is the same reason automation runs left to right here: a hand recognises a
/// signal path that flows the way it reads.
/// </remarks>
public enum PatchSide
{
    /// <summary>Audio coming in, drawn down the left edge.</summary>
    In,

    /// <summary>Audio going out, drawn down the right edge.</summary>
    Out
}
