using System.Collections.Generic;

namespace JingleBox2.Machines;

/// <summary>
/// The options a machine's Menu part can carry, one literal each.
/// </summary>
/// <remarks>
/// A Menu is a generic part: the machine says it wants one and which options are on it, and the
/// host fills those options in. Two today and there will be more, which is the whole reason the
/// part is not called after either of them and the words live here rather than being built into
/// the drawing.
///
/// Written out as constants rather than an enum, the same as
/// <see cref="MachineElementKinds"/> and for the same reason: a machine may name an option this
/// host has never heard of, and the answer is to leave that line out rather than to refuse the
/// file. A machine naming none carries every option there is, which is what a Menu dropped on a
/// panel and left alone should do.
/// </remarks>
public static class MachineMenuOptions
{
    /// <summary>The control surfaces there is a template for on this machine, one line each.</summary>
    /// <remarks>
    /// Picking one points that controller at this machine the way its template says. What a
    /// template is, and which of them name this machine, is the host's to answer.
    /// </remarks>
    public const string Surfaces = "surfaces";

    /// <summary>Start or stop learning, which is the same mode Ctrl+Shift+M turns over.</summary>
    public const string Learn = "learn";

    /// <summary>What a Menu carries when it names no options, which is all of them.</summary>
    /// <remarks>
    /// In the order they are offered, so a designer listing them and a panel drawing them agree
    /// without either being told about the other.
    /// </remarks>
    public static readonly IReadOnlyList<string> All = new[] { Surfaces, Learn };

    /// <summary>What the property naming them is called in a machine's file.</summary>
    public const string Property = "options";

    /// <summary>What separates them in that property.</summary>
    public const char Between = ',';
}
