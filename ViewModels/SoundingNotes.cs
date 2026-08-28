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
/// Shared by both designers rather than written twice, since the rack page and a track's
/// window light their keys the same way.
/// </remarks>
public sealed class SoundingNotes
{
    /// <summary>
    /// How often the lit keys are counted down, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Twenty-five beats a second, which is finer than a key going out can be seen and coarse
    /// enough that a room full of nothing costs nothing. A note's length is turned into a whole
    /// number of these and is never fewer than one, so the shortest thing anybody can play still
    /// lights its key for long enough to be seen.
    /// </remarks>
    private const int TickMs = 40;

    /// <summary>How many beats each lit semitone has left before it goes out.</summary>
    private readonly Dictionary<int, int> _left = new();

    /// <summary>
    /// Runs exactly while something is lit, started by the first note and stopped by the last.
    /// </summary>
    /// <remarks>
    /// Deliberately not a timer left running: the panels this serves are open for a whole
    /// session and are usually looking at silence.
    /// </remarks>
    private readonly DispatcherTimer _clock;

    /// <summary>
    /// What the track's own voice is sounding, which is at most one thing.
    /// </summary>
    /// <remarks>
    /// A track has one voice, so a note it plays puts out the note it played before rather
    /// than lighting beside it. Notes played by hand pile up, because auditions do.
    /// </remarks>
    private int _alone = -1;

    /// <summary>Sets the clock up and leaves it stopped, since nothing is lit yet.</summary>
    public SoundingNotes()
    {
        _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        _clock.Tick += (_, _) => Tick();
    }

    /// <summary>The semitones lit now. The keyboard follows this as it changes.</summary>
    public ObservableCollection<int> Lit { get; } = new();

    /// <summary>
    /// Raised on every beat of the clock, and once the moment a note is struck.
    /// </summary>
    /// <remarks>
    /// Something is sounding exactly while this clock is running, so anything else that has to
    /// keep up with a sounding note can hang off it rather than starting a timer of its own and
    /// leaving it ticking over an empty room.
    /// </remarks>
    public event Action? Ticked;

    /// <summary>
    /// A note has just been struck by hand, for anything that should follow what was played.
    /// </summary>
    /// <remarks>
    /// Notes from a track are left out: a panel whose selection jumped about on its own while
    /// a song played would be unusable, and what a track plays is already on the pattern.
    /// </remarks>
    public event Action<Note>? Hit;

    /// <summary>A note has just been played, and should light for as long as it sounds.</summary>
    /// <param name="note">Which key to light, and an unplayable one for an OFF row, which puts a key out.</param>
    /// <param name="seconds">
    /// How long it will sound, so the key goes out on its own. Zero where nobody knows, which
    /// leaves it lit until something else puts it out.
    /// </param>
    /// <param name="alone">
    /// True for a note from a track, which has one voice: it puts out whatever that track was
    /// sounding. False for a note played by hand, which piles up with the others.
    /// </param>
    /// <remarks>
    /// Notes arrive from the clock thread, and <see cref="Lit"/> is what a keyboard draws from,
    /// so anything off the drawing thread is posted to it rather than touching the collection
    /// where it stands.
    ///
    /// A track's voice stops before the next one starts, so its key goes out first. An OFF row
    /// arrives here as a note that cannot be played, which is exactly that and no more: the key
    /// goes out and nothing is lit in its place.
    ///
    /// Struck again while still lit, a key stays lit from now rather than from when it was
    /// first hit, so a note repeated quickly does not go dark under the second press.
    ///
    /// <see cref="Ticked"/> is raised at once as well as on the clock, so a cursor that follows
    /// it appears with the note rather than a fortieth of a second after it.
    /// </remarks>
    public void Struck(Note note, double seconds, bool alone = false)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Struck(note, seconds, alone));
            return;
        }

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

        _left[note.Semitone] = ticks;

        if (!_clock.IsEnabled) _clock.Start();

        Ticked?.Invoke();

        if (!alone) Hit?.Invoke(note);
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

        Ticked?.Invoke();
    }

    /// <summary>
    /// Counts every lit key down one beat and puts out the ones that have run out.
    /// </summary>
    /// <remarks>
    /// The keys are copied before they are walked, since a key going out is taken out of the
    /// dictionary being read. Nothing sounding is nothing to count down, so the clock stops
    /// rather than idling over an empty room.
    /// </remarks>
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

        if (_left.Count == 0) _clock.Stop();

        Ticked?.Invoke();
    }
}
