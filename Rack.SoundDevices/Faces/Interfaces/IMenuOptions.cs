using System.Collections.Generic;

namespace JingleBox2.Rack.SoundDevices.Faces.Interfaces;

/// <summary>
/// Which options a sound device's Menu part carries, read off what its file says.
/// </summary>
/// <remarks>
/// The rule on its own, so it can be put a question to without a window, a sound device or a host.
/// Everything above it is drawing and everything below it is a decision, which is the same split
/// the pointing gesture's own rule is kept on.
///
/// It is deliberately forgiving in one direction and strict in the other. A sound device naming an
/// option this build has never heard of is a sound device from a later version, and the answer is
/// to carry the ones that are understood rather than to refuse the part; a sound device naming
/// nothing at all carries every option there is, which is what a Menu dropped on a panel and left
/// alone should do.
/// </remarks>
public interface IMenuOptions
{
    /// <summary>
    /// The options that text names, or all of them when there is no text at all.
    /// </summary>
    /// <remarks>
    /// Nothing and an empty list are two different answers. No property means the sound device has
    /// said nothing about which options it wants, which is all of them; a property present and
    /// empty means somebody has taken every option off, which is a menu that drops down nothing
    /// and is a state the designer allows.
    /// </remarks>
    /// <param name="said">What the file says, or nothing where it says nothing.</param>
    IReadOnlyList<string> Named(string? said);

    /// <summary>Whether a line belonging to that option is carried.</summary>
    /// <remarks>
    /// A line belonging to no option is always carried: it is something the Menu always says
    /// rather than part of an option anybody chose.
    /// </remarks>
    /// <param name="named">What <see cref="Named"/> answered.</param>
    /// <param name="option">Which option the line belongs to, or nothing.</param>
    bool Carries(IReadOnlyList<string> named, string? option);
}
