using System.Collections.Generic;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.Shortcuts.Records;

namespace JingleBox2.Shortcuts;

/// <inheritdoc/>
public sealed class SystemKeys : ISystemKeys
{
    /// <summary>What each of the four that go through the map is called.</summary>
    private readonly IShortcutActions _actions;

    /// <summary>Which key each of those is on, or nothing to ask the application's own.</summary>
    private readonly IShortcutMap? _map;

    /// <summary>Builds one over the application's own map unless another is handed in.</summary>
    /// <param name="actions">What each shortcut is called, or nothing for the ordinary list.</param>
    /// <param name="map">Which key each is on, or nothing for the application's own.</param>
    /// <param name="pattern">What the pattern answers, or nothing for the ordinary table.</param>
    public SystemKeys(IShortcutActions? actions = null, IShortcutMap? map = null, IPatternKeys? pattern = null)
    {
        _actions = actions ?? new ShortcutActions();
        _map = map;
        _pattern = pattern ?? new PatternKeys();
    }

    /// <summary>Whichever map this list is about.</summary>
    private IShortcutMap Map => _map ?? ShortcutKeys.Map;

    /// <inheritdoc/>
    /// <remarks>
    /// The doors first, since those are the keys somebody uses while playing rather than while
    /// editing, and they are the ones that work in every window.
    /// </remarks>
    public IReadOnlyList<SystemKey> All
    {
        get
        {
            var keys = new List<SystemKey>
            {
                new("Space", "starts the transport and stops it again"),
                new("Ctrl+R", "starts a recording and stops it again"),
                new("Ctrl+Shift+M", "turns the pointing mode over, for aiming a knob on your hardware at a control"),
                new("Ctrl+H", "opens the help on the keys")
            };

            foreach (var (action, _, _) in _actions.Everything)
            {
                if (!_actions.Fixed(action)) continue;

                string on = Map.Said(action);

                if (on.Length > 0) keys.Add(new SystemKey(on, Does(action)));
            }

            keys.AddRange(Pattern);

            return keys;
        }
    }

    /// <summary>
    /// The keys the pattern answers for itself, which go through no map at all.
    /// </summary>
    /// <remarks>
    /// **A third place these come from, and the reason the list exists.** The first four are
    /// doors hung on every window and the middle ones are the map's fixed actions; these are
    /// written into the tracker's own key handling, which is what makes them unchangeable in the
    /// same way and is exactly why they belong here rather than in the map. Putting one in the
    /// map to get it onto this card would be two ways of delivering one keystroke, which is the
    /// fault this list was made to end.
    ///
    /// Read off <see cref="IPatternKeys"/> rather than written out, which is the whole point of
    /// that table: the key, the words and what it does are one row, so a key added to the pattern
    /// arrives on this card and in the help without anybody being told. Written out here it was
    /// the same key said twice, once in the view's own handling and once in a list beside it.
    /// </remarks>
    private IReadOnlyList<SystemKey> Pattern => _pattern.Listed;

    /// <summary>The keys the pattern answers, which say themselves.</summary>
    private readonly IPatternKeys _pattern;

    /// <summary>What one of the map's four does, in the words a list row wants.</summary>
    /// <param name="action">The shortcut being described.</param>
    private static string Does(ShortcutAction action) => action switch
    {
        ShortcutAction.Save => "writes down whatever the page you are on owns",
        ShortcutAction.Delete => "takes away whatever is picked out",
        ShortcutAction.Undo => "puts back the last thing you did on this page",
        ShortcutAction.Redo => "does the last undone thing over again",
        _ => ""
    };
}
