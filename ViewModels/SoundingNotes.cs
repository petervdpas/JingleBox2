using Avalonia.Threading;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// Which notes are sounding, for a keyboard that shows them.
/// </summary>
/// <remarks>
/// The engine does not report when a voice finishes, and asking it would mean the panel
/// knowing about voices. So a note is held lit for as long as it was asked to sound and then
/// let go, which is exactly right for an audition and close enough for a pattern: a key that
/// stayed lit after its note had died would be worse than one that goes out a moment early.
///
/// Shared by both designers rather than written twice, since the library page and a track's
/// window light their keys the same way.
/// </remarks>
public sealed class SoundingNotes
{
    private const int TickMs = 40;

    private readonly Dictionary<int, int> _left = new();
    private readonly DispatcherTimer _clock;

    /// <summary>
    /// What the track's own voice is sounding, which is at most one thing.
    /// </summary>
    /// <remarks>
    /// A track has one voice, so a note it plays puts out the note it played before rather
    /// than lighting beside it. Notes played by hand pile up, because auditions do.
    /// </remarks>
    private int _alone = -1;

    public SoundingNotes()
    {
        _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        _clock.Tick += (_, _) => Tick();
    }

    /// <summary>The semitones lit now. The keyboard follows this as it changes.</summary>
    public ObservableCollection<int> Lit { get; } = new();

    /// <summary>A note has just been played, and should light for as long as it sounds.</summary>
    /// <param name="alone">
    /// True for a note from a track, which has one voice: it puts out whatever that track was
    /// sounding. False for a note played by hand, which piles up with the others.
    /// </param>
    public void Struck(Note note, double seconds, bool alone = false)
    {
        // Off the clock thread this would touch the collection a keyboard is drawing from.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Struck(note, seconds, alone));
            return;
        }

        // A track's voice stops before the next one starts, so its key goes out first. An OFF
        // row arrives here as a note that cannot be played, which is exactly that and no more.
        if (alone && _alone >= 0)
        {
            _left.Remove(_alone);
            Lit.Remove(_alone);
            _alone = -1;
        }

        if (!note.IsPlayable) return;

        if (alone) _alone = note.Semitone;

        int ticks = Math.Max(1, (int)Math.Round(seconds * 1000 / TickMs));

        if (!_left.ContainsKey(note.Semitone)) Lit.Add(note.Semitone);

        // Struck again while still lit, it stays lit from now rather than from last time.
        _left[note.Semitone] = ticks;

        if (!_clock.IsEnabled) _clock.Start();
    }

    /// <summary>Everything goes dark, for a transport that has stopped.</summary>
    public void Silence()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Silence);
            return;
        }

        _left.Clear();
        Lit.Clear();
        _alone = -1;
        _clock.Stop();
    }

    private void Tick()
    {
        foreach (int semitone in _left.Keys.ToList())
        {
            int left = _left[semitone] - 1;

            if (left > 0)
            {
                _left[semitone] = left;
                continue;
            }

            _left.Remove(semitone);
            Lit.Remove(semitone);

            if (_alone == semitone) _alone = -1;
        }

        // Nothing sounding is nothing to count down, so the timer stops rather than idling.
        if (_left.Count == 0) _clock.Stop();
    }
}
