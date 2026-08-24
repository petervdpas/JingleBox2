using Avalonia.Input;
using System.Collections.Generic;

namespace JingleBox2.UI;

/// <summary>
/// One press is one press, however long the key is held down.
/// </summary>
/// <remarks>
/// A held key does not send one message. X11 and Windows both send a stream of key-downs for
/// as long as it is held, some thirty a second once the first half second is up, and a handler
/// that takes each of them for a press turns holding the space bar into starting and stopping
/// the transport thirty times a second. What you hear is the first note of the song, again and
/// again, which is exactly what it is.
///
/// Neither platform sends a key-up between those repeats, X11 because Avalonia asks it not to,
/// so a key that is already down is a repeat and nothing else. Avalonia's key arguments do not
/// carry the flag that would say so, which is the whole reason this is here.
///
/// One of these per window, asked about every key that window handles itself, so a shortcut
/// added later gets the same treatment without having to remember this again.
/// </remarks>
public sealed class HeldKeys
{
    private readonly HashSet<Key> _down = new();

    /// <summary>True the first time a key goes down, false for every repeat after it.</summary>
    public bool Pressed(Key key) => _down.Add(key);

    /// <summary>Back up again, so the next press counts.</summary>
    public void Released(Key key) => _down.Remove(key);

    /// <summary>
    /// Forgets everything held.
    /// </summary>
    /// <remarks>
    /// For the moment the window stops being the one keys are going to. The key-up will be
    /// delivered wherever the keys went instead, this window will never hear it, and without
    /// this the key would look held down for the rest of the session.
    /// </remarks>
    public void Forget() => _down.Clear();
}
