using System.Collections.Generic;

namespace JingleBox2.Rack.SoundDevices.Faces;

/// <summary>
/// Where a machine's Menu part sits, one literal each.
/// </summary>
/// <remarks>
/// A Menu is not laid out with the machine's controls and cannot be: it is the one place the
/// program itself speaks on somebody's front panel, so it is drawn over the panel rather than in
/// it, and wherever it is dropped in the tree makes no difference to where it appears.
///
/// Two of them, and the two at the top. The top right is the default, since that is where every
/// program has ever put this button and therefore the first place a hand looks, and the top left
/// is there for a machine whose own artwork wants that side.
///
/// The bottom two were offered for a while and had to go. A panel taller than the window it is
/// shown in scrolls, and the bottom of the panel is then below the fold: the button was really
/// there and nobody could see it, which reads exactly like a machine that has not been updated.
/// A part that can be placed where it cannot be found is a part with a trap in it.
/// </remarks>
public static class MenuCorners
{
    /// <summary>The top left of the panel.</summary>
    public const string TopLeft = "topLeft";

    /// <summary>The top right of the panel, which is where one goes unless somebody says otherwise.</summary>
    public const string TopRight = "topRight";

    /// <summary>Both of them, in the order they are offered.</summary>
    public static readonly IReadOnlyList<string> All = new[] { TopRight, TopLeft };

    /// <summary>What the property naming it is called in a machine's file.</summary>
    public const string Property = "corner";
}
