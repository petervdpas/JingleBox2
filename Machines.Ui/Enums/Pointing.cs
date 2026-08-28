namespace JingleBox2.Machines.Ui.Enums;

/// <summary>Which way a triangular cap points.</summary>
public enum Pointing
{
    /// <summary>The default, since a triangle on a panel is usually a next or a play.</summary>
    Right = 0,

    /// <summary>Back, or previous.</summary>
    Left = 1,

    /// <summary>Up, which on a panel is usually more of something.</summary>
    Up = 2,

    /// <summary>And down, less of it.</summary>
    Down = 3
}
