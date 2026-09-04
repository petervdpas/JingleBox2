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
    public SystemKeys(IShortcutActions? actions = null, IShortcutMap? map = null)
    {
        _actions = actions ?? new ShortcutActions();
        _map = map;
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
                new("Ctrl+R", "records"),
                new("Ctrl+Shift+M", "turns the pointing mode over, for aiming a knob on your hardware at a control"),
                new("Ctrl+H", "opens the help on the keys")
            };

            foreach (var (action, _, _) in _actions.Everything)
            {
                if (!_actions.Fixed(action)) continue;

                string on = Map.Said(action);

                if (on.Length > 0) keys.Add(new SystemKey(on, Does(action)));
            }

            return keys;
        }
    }

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
