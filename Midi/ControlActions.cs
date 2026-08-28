using System;

namespace JingleBox2.Midi;

/// <summary>
/// Where a hardware button's press comes out, for the machine showing that panel to act on.
/// </summary>
/// <remarks>
/// A knob points at a parameter and the parameter is a thing anything can write: the value is
/// on the instrument and moving it needs no panel. A button points at an action, and an action
/// is not a value anywhere. Chopping a recording, adding a zone, clearing a pad: these reach
/// past a machine's settings into dialogs, shelves and grids, which is why a described panel
/// asks for them rather than doing them.
///
/// So a mapped button says what was pressed and the panel showing that machine does it, exactly
/// as if the button on screen had been clicked. Which is the honest limit of it: an action goes
/// where the panel is. A knob mapped to a filter works with the panel shut, because the filter
/// is on the instrument; a pad mapped to Chop works while you are looking at the machine, which
/// is when anybody wants to chop anything.
///
/// One of these for the session, in the way of <see cref="ControlLink"/> and for the same
/// reason: the panels that have to hear it are drawn from a description and know nothing of
/// view models.
/// </remarks>
public sealed class ControlActions
{
    /// <summary>
    /// The one this session is using.
    /// </summary>
    /// <remarks>
    /// A static, and the same reason as <see cref="ControlLink.Current"/>: the panels that have
    /// to hear this are drawn from a description and have never heard of a view model, so
    /// threading a reference through every designer, window and panel would be a great deal of
    /// wiring to express something that is true of the application rather than of any part of it.
    /// </remarks>
    public static ControlActions Current { get; } = new();

    /// <summary>
    /// A mapped button was pressed: which machine it was pointed at, and what it asks for.
    /// </summary>
    /// <remarks>
    /// The machine as well as the action, because two machines both have a Clear and they clear
    /// different things. A panel showing another machine leaves it alone.
    /// </remarks>
    public event Action<string, string>? Fired;

    /// <summary>
    /// Says a mapped button was pressed, for whichever panel is showing that machine.
    /// </summary>
    /// <remarks>
    /// An empty action is dropped rather than announced, since a panel matching on the name
    /// would have to guard against it and every panel would have to remember to.
    /// </remarks>
    public void Fire(string machine, string action)
    {
        if (string.IsNullOrEmpty(action)) return;

        Fired?.Invoke(machine ?? "", action);
    }
}
