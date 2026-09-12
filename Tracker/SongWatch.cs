using System;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// Holds two things and writes a line. Nothing about a song, a page or a disc is in here, which
/// is what lets the whole of it be put a question to without any of the three.
/// </remarks>
public sealed class SongWatch : ISongWatch
{
    /// <inheritdoc/>
    public bool Unsaved { get; private set; }

    /// <inheritdoc/>
    public string Because { get; private set; } = "";

    /// <inheritdoc/>
    public event Action? Moved;

    /// <inheritdoc/>
    /// <remarks>
    /// Said again whenever it is a different edit from the last rather than only the first time,
    /// since the second cause would otherwise be silent behind the first: a song made unsaved by
    /// typing a note and then quietly changed by something nobody asked for would read as the
    /// note all the way through.
    /// </remarks>
    public void Changed(string what)
    {
        string said = what ?? "";

        bool moved = !Unsaved;

        if (!Unsaved || !string.Equals(Because, said, StringComparison.Ordinal))
            Log.Write(LogArea.Tracker, () => "the song has something unsaved in it now: " + said);

        Unsaved = true;
        Because = said;

        if (moved) Moved?.Invoke();
    }

    /// <inheritdoc/>
    public void Saved()
    {
        if (!Unsaved)
        {
            Because = "";
            return;
        }

        Unsaved = false;
        Because = "";

        Moved?.Invoke();
    }
}
