using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Shortcuts;

/// <summary>
/// Delivers a keystroke to whatever is in front of you that wants it.
/// </summary>
/// <remarks>
/// Every window listens, and the answer comes from the thing with the keyboard rather than from
/// a register of which page is open. Starting at what has focus and walking outwards, the first
/// thing that says it can do the action does it, and if nothing says so the key carries on as
/// though this were not here. That is what makes it context sensitive without anything having to
/// be told when the context changed: a dialog answers because the dialog is where the focus is,
/// and closing it changes the answer by itself.
///
/// This knows nothing about what any action means and nothing about what any key is. The first
/// is <see cref="IShortcutContext"/> and the second is <see cref="ShortcutMap"/>, and that split
/// is the point: a page that edits shortcuts edits the map, and nothing here changes.
/// </remarks>
public static class ShortcutKeys
{
    /// <summary>
    /// What the keys are set to. Replaced when the settings change them.
    /// </summary>
    /// <remarks>
    /// One map for the application, since a keystroke means the same thing wherever you press
    /// it; what differs is who answers, and that is <see cref="IShortcutContext"/>.
    /// </remarks>
    public static IShortcutMap Map { get; set; } = new ShortcutMap();

    /// <summary>
    /// Has a window answer shortcuts. Every window, including the dialogs.
    /// </summary>
    /// <remarks>
    /// Heard on the way down, like the space bar and the pointing mode, so a control that would
    /// otherwise spend the keystroke does not get the chance. What it must not do is take one
    /// nobody wants, which is why nothing is marked handled unless a context said yes first.
    /// </remarks>
    public static void Listen(InputElement window)
    {
        if (window is null) return;

        window.AddHandler(InputElement.KeyDownEvent, Pressed, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// A key arrived: work out what it asks for, find who can do it, and let them.
    /// </summary>
    /// <remarks>
    /// **Save is the only one answered while a caret is blinking.** Undo in a box somebody is
    /// typing in is the box's own undo, and taking it would be taking away the only thing that
    /// keystroke has ever meant there. Saving while typing a name is a perfectly sensible thing
    /// to ask for, and no text box has ever done anything with Ctrl+S.
    ///
    /// A page shortcut is refused there for a different reason, and it is the one that decided
    /// the rule rather than a list of three actions. A page key is Ctrl+Alt and a letter, and on
    /// a good many keyboard layouts AltGr is delivered as exactly that: on a Dutch or a German
    /// layout the characters behind AltGr are how somebody types a bracket or a euro sign. A
    /// name that jumped to another page halfway through being typed would be the worst kind of
    /// fault, since nothing on the screen would say why.
    ///
    /// A page that throws is written down and nothing more. One page that will not do a thing
    /// is one page, not a broken keyboard.
    /// </remarks>
    private static void Pressed(object? sender, KeyEventArgs e)
    {
        if (e.Handled || LearningKeys.On) return;
        if (Map.Match(e.Key, e.KeyModifiers) is not { } action) return;

        if (Typing(sender) && action != ShortcutAction.Save) return;

        if (Asked(sender, action) is not { } context) return;

        try
        {
            context.Do(action);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.App, () => "shortcuts: " + action + " threw: " + bad.Message);
        }

        e.Handled = true;
    }

    /// <summary>True when a caret is blinking somewhere and the key is probably meant for it.</summary>
    private static bool Typing(object? sender) =>
        (sender as TopLevel ?? TopLevel.GetTopLevel(sender as Visual))
        ?.FocusManager?.GetFocusedElement() is TextBox;

    /// <summary>
    /// The nearest thing to the keyboard that says it can do this, or nothing.
    /// </summary>
    /// <remarks>
    /// Outwards from the focus, and the control itself before its settings, so a view can answer
    /// for itself where that is simpler and hand over to its view model where it is not. The
    /// window's own settings are the last thing asked, which is what makes a page-wide answer
    /// possible without every control on it knowing: a page that is not where the focus happens
    /// to be is still the page you are looking at.
    /// </remarks>
    private static IShortcutContext? Asked(object? sender, ShortcutAction action)
    {
        var top = sender as TopLevel ?? TopLevel.GetTopLevel(sender as Visual);

        var at = top?.FocusManager?.GetFocusedElement() as Visual ?? top;

        while (at is not null)
        {
            if (at is IShortcutContext itself && itself.Can(action)) return itself;

            if (at is StyledElement { DataContext: IShortcutContext behind } && behind.Can(action))
                return behind;

            at = at.GetVisualParent();
        }

        return top is StyledElement { DataContext: IShortcutContext window } && window.Can(action)
            ? window
            : null;
    }
}
