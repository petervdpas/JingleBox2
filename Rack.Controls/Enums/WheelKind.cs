namespace JingleBox2.Rack.Controls.Enums;

/// <summary>Which of the two wheels beside a keyboard a <see cref="Wheel"/> is showing.</summary>
/// <remarks>
/// There are two and there have only ever been two, so this is a closed list rather than a name
/// somebody supplies. It says two things at once and they are the same thing said twice: which
/// number the wheel reads, and where it rests when nobody is touching it.
/// </remarks>
public enum WheelKind
{
    /// <summary>The pitch wheel: rests in the middle and leans either way.</summary>
    Pitch = 0,

    /// <summary>The modulation wheel: rests at the bottom and only goes up.</summary>
    Modulation = 1
}
