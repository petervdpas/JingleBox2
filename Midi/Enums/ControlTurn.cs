namespace JingleBox2.Midi.Enums;

/// <summary>How an endless encoder says which way it was turned.</summary>
/// <remarks>
/// There is no standard, only two conventions, and a controller sending one read as the other
/// turns the wrong way and jumps the length of the range doing it. Which one this is gets
/// worked out along with everything else: an encoder resting at the middle of the range is
/// counting from there, and one resting at either end is counting in two's complement.
/// </remarks>
public enum ControlTurn
{
    /// <summary>Middle of the range is still, above is clockwise, below is anticlockwise.</summary>
    Offset,

    /// <summary>Small numbers are clockwise, large ones are anticlockwise and count down from 128.</summary>
    Twos
}
