using System.Collections.Generic;
using System.Linq;
using JingleBox2.Help.Interfaces;
using JingleBox2.Shortcuts;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Help;

/// <inheritdoc/>
/// <remarks>
/// The keys are grouped by where they work rather than by what they do, since that is the
/// question somebody has when they open this: I am looking at the pattern, what can I press.
/// </remarks>
public sealed class ShortcutSheet : IShortcutSheet
{
    /// <summary>What each editable shortcut is called.</summary>
    private readonly IShortcutActions _actions;

    /// <summary>
    /// Which key each of those is on, or nothing to ask the application's own map.
    /// </summary>
    /// <remarks>
    /// Held rather than read once, because the map is replaced when the settings are read and
    /// a page built at startup would be a page of defaults for the rest of the session.
    /// </remarks>
    private readonly IShortcutMap? _map;

    /// <summary>Every key the application answers that is not yours to change.</summary>
    private readonly ISystemKeys _system;

    /// <summary>Builds one, over the application's own map unless another is handed in.</summary>
    /// <param name="actions">What each editable shortcut is called.</param>
    /// <param name="map">Which key each is on, or nothing for the application's.</param>
    /// <param name="system">The keys nobody may change, or nothing for the ordinary list.</param>
    public ShortcutSheet(IShortcutActions? actions = null, IShortcutMap? map = null, ISystemKeys? system = null)
    {
        _actions = actions ?? new ShortcutActions();
        _map = map;
        _system = system ?? new SystemKeys(_actions, map);
    }

    /// <summary>Whichever map this page is about.</summary>
    private IShortcutMap Map => _map ?? ShortcutKeys.Map;

    /// <inheritdoc/>
    public string System =>
        string.Join("\n", _system.All.Select(one => "- `" + one.Keys + "` " + one.Does + "."));

    /// <inheritdoc/>
    public string Menu => string.Join("\n", Said(fixedOnes: false));

    /// <summary>
    /// Every shortcut, said as it stands now.
    /// </summary>
    /// <remarks>
    /// Every action is walked rather than any of them being named, so one added later turns up
    /// here without anybody being told to add it, the way the log's page builds itself from the
    /// areas the log knows about.
    ///
    /// A page on no key says so rather than being left out. Left out, the list would be four
    /// lines on a fresh installation and there would be nothing on the page saying that a key
    /// can be put on a page at all.
    /// </remarks>
    /// <param name="fixedOnes">True for the system's, false for the pages.</param>
    private IEnumerable<string> Said(bool fixedOnes)
    {
        foreach (var one in _actions.Everything)
        {
            if (_actions.Fixed(one.Action) != fixedOnes) continue;

            string key = Map.Said(one.Action);

            yield return key.Length > 0
                ? "- `" + key + "` " + Does(one.Action)
                : "- " + one.Name + " is on no key.";
        }
    }

    /// <summary>
    /// What one of them does, in the words the help page uses.
    /// </summary>
    /// <remarks>
    /// Written out case by case rather than taken from the action's name, because the name is a
    /// word on a settings row and this is a sentence in a paragraph. An action added here and
    /// not given a sentence falls back to its name, which is plain rather than broken.
    /// </remarks>
    /// <param name="action">The shortcut being described.</param>
    private string Does(ShortcutAction action) => action switch
    {
        ShortcutAction.Save => "writes down whatever the page you are on owns: a song, a machine, the pads.",
        ShortcutAction.Delete => "takes away whatever is picked out, where the page has something to pick out.",
        ShortcutAction.Undo => "puts back the last thing you did on this page.",
        ShortcutAction.Redo => "does the last undone thing over again.",
        _ => "goes to " + _actions.Named(action) + "."
    };
}
