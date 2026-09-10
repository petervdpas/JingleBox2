namespace JingleBox2.Audio.Routing.Enums;

/// <summary>What became of taking the chosen source off its own output.</summary>
/// <remarks>
/// **Three answers rather than a bool, because a bool ran two of them together.** Not trying and
/// trying and failing are different things and want opposite wording: one is the ordinary case and
/// deserves no sentence at all, the other is a machine that could not do what was asked and is
/// worth saying out loud. Answered as a bool, the quiet case was reported as the loud one.
///
/// Nothing here is a number anybody writes down, so the order is free.
/// </remarks>
public enum InputAside
{
    /// <summary>Nothing was attempted: there is no source, or it could not be heard anyway.</summary>
    Nothing,

    /// <summary>It was taken off its own output and is on the desk alone.</summary>
    Moved,

    /// <summary>It was asked for and the machine could not do it.</summary>
    Refused
}
