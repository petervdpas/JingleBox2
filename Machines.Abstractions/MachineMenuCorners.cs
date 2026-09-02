using System.Collections.Generic;

namespace JingleBox2.Machines;

/// <summary>
/// Where a machine's Menu part sits, one literal each.
/// </summary>
/// <remarks>
/// A Menu is not laid out with the machine's controls and cannot be: it is the one place the
/// program itself speaks on somebody's front panel, so it is drawn over the panel rather than in
/// it, and wherever it is dropped in the tree makes no difference to where it appears.
///
/// Four of them, because a corner is a corner: a machine with its name badge in one and its logo
/// across another has to be able to put this wherever it is out of the way. The top right is the
/// default, since that is where every program has ever put this button and therefore the first
/// place a hand looks.
/// </remarks>
public static class MachineMenuCorners
{
    /// <summary>The top left of the panel.</summary>
    public const string TopLeft = "topLeft";

    /// <summary>The top right of the panel, which is where one goes unless somebody says otherwise.</summary>
    public const string TopRight = "topRight";

    /// <summary>The bottom left of the panel.</summary>
    public const string BottomLeft = "bottomLeft";

    /// <summary>The bottom right of the panel.</summary>
    public const string BottomRight = "bottomRight";

    /// <summary>All four, in the order they are offered.</summary>
    public static readonly IReadOnlyList<string> All = new[] { TopRight, TopLeft, BottomRight, BottomLeft };

    /// <summary>What the property naming it is called in a machine's file.</summary>
    public const string Property = "corner";
}
