using Avalonia.Threading;
using JingleBox2.Machines;
using JingleBox2.Midi;
using JingleBox2.Tracker;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;

namespace JingleBox2.ViewModels;

/// <summary>
/// The keyboard on a machine's own face, answered by whoever is showing the panel.
/// </summary>
/// <remarks>
/// The keyboard was the app's for as long as it was the same on every machine: one at the foot
/// of the panel, playing whatever was in front of it. On a kit it is not the same. Sixteen keys
/// out of a hundred and twenty do anything, and which key fires which drum is the question the
/// keyboard is there to answer, so the machine puts one on its own face and this is what stands
/// behind it.
///
/// Two pictures of one thing. The pads and the keys are the same sixteen drums, so picking a pad
/// outlines its key and hitting a key lights its pad, and neither of those is arranged anywhere:
/// they are the same fact read twice.
///
/// It follows rather than copies. What is on the pads is the kit's own list and which keys are
/// down is the monitor's, and this holds neither: a second copy would be wrong the first time a
/// note was played.
/// </remarks>
public sealed class DesignerKeys : IMachineKeys, IDisposable
{
    /// <summary>How wide a panel's keyboard is, and where it has to be to show a note.</summary>
    private readonly IPanelKeyboard _keyboard = new PanelKeyboard();

    /// <summary>
    /// Whoever is showing the panel: the octave, the instrument being edited, and the means to
    /// sound a note.
    /// </summary>
    /// <remarks>
    /// Asked rather than held, because the instrument under a designer changes while the keyboard
    /// stays where it is.
    /// </remarks>
    private readonly IInstrumentDesigner _designer;

    /// <summary>
    /// Which keys are down, from every producer there is.
    /// </summary>
    /// <remarks>
    /// The monitor rather than a set of this keyboard's own, because a keyboard drawn on a panel
    /// is a picture of the keys and not a record of the presses this particular panel happened
    /// to hear. A panel that kept its own showed nothing for the hardware, which reaches the
    /// stream and never reaches a panel; one that listened only while its page was in front, or
    /// only while the cursor was on its track, would go on being wrong in quieter ways.
    ///
    /// Its own when nobody hands one over, so a panel on the bench and a panel in a test are
    /// keyboards like any other.
    /// </remarks>
    private readonly IMidiMonitor _keys;

    /// <summary>The kit currently being listened to, so it can be let go when another arrives.</summary>
    private DrumKitViewModel? _watching;

    /// <summary>
    /// Wires the keyboard to the application's monitor, or to one of its own where there is none.
    /// </summary>
    /// <remarks>
    /// Read once on the way in, since a window opened while a chord is held has to show the chord
    /// rather than wait for the next key.
    /// </remarks>
    public DesignerKeys(IInstrumentDesigner designer)
    {
        _designer = designer;
        _keys = designer.MidiKeys ?? new MidiMonitor();

        Read();

        _keys.Changed += Moved;
    }

    /// <summary>
    /// The keys with a hand on them: what the keyboard lights.
    /// </summary>
    /// <remarks>
    /// Keys, not sound. A note is an event with two halves and a sound is a thing with a length,
    /// and this is a picture of the first: what is sounding is the question a kit's pads answer,
    /// and a cymbal rings for four seconds after the key that started it came up.
    ///
    /// One collection for the life of this, written into rather than replaced, because both
    /// keyboards watch it for changes and a fresh list on every read is a list nothing can
    /// watch.
    /// </remarks>
    public IEnumerable Lit => _lit;

    /// <inheritdoc cref="Lit"/>
    private readonly ObservableCollection<int> _lit = new();

    /// <summary>
    /// The keys with something on them, which on anything but a kit is none of them.
    /// </summary>
    /// <remarks>
    /// A machine that answers every key answers with nothing here, because a keyboard with all
    /// hundred and twenty keys banded says exactly as much as one with none.
    /// </remarks>
    public IEnumerable Filled
    {
        get
        {
            Follow();

            if (Kit is not { } kit) return Array.Empty<int>();

            return kit.Pads.Where(pad => pad.HasSound).Select(pad => pad.Semitone).ToList();
        }
    }

    /// <summary>The key belonging to the pad in hand, so the grid and the keyboard agree.</summary>
    public int Marked
    {
        get
        {
            Follow();

            return Kit?.Selected?.Semitone ?? -1;
        }
    }

    /// <summary>Where the keyboard is looking, which is the designer's own and not the song's.</summary>
    public int Octave
    {
        get => _designer.Octave;
        set
        {
            if (_designer.Octave == value) return;

            _designer.Octave = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Plays it, and says so, which is what clicking a key has always done.</summary>
    /// <remarks>
    /// A key already down is not played again. Holding one on the computer keyboard repeats it
    /// for as long as it is held, and a machine retriggered forty times a second is not what
    /// anybody meant by leaning on a key.
    /// </remarks>
    public void Play(int semitone)
    {
        if (_keys.Holds(semitone)) return;

        _keys.Pressed(semitone);

        _designer.Play(new Note(semitone), TrackerCell.NoVolume);
    }

    /// <summary>
    /// Says it is up again, and lets the note go.
    /// </summary>
    /// <remarks>
    /// The release and not a stop: what was started goes into its release the way it does when a
    /// pattern reaches an OFF, so a sound with a long tail keeps its tail.
    /// </remarks>
    public void Let(int semitone)
    {
        if (!_keys.Holds(semitone)) return;

        _keys.Released(semitone);

        _designer.Let(new Note(semitone));
    }

    /// <summary>Raised when anything the keyboard draws itself from moved.</summary>
    /// <remarks>
    /// One event for all of it: the keys down, the pad in hand, and the octave on show. A keyboard
    /// reads itself again when it hears this, and reading it is a handful of comparisons.
    /// </remarks>
    public event EventHandler? Changed;

    /// <summary>Stops listening, for a panel nobody can reach any more.</summary>
    /// <remarks>
    /// The monitor outlives every panel, so a window closed with nothing taken off it would go
    /// on being told about keys for the rest of the session.
    /// </remarks>
    public void Dispose() => _keys.Changed -= Moved;

    /// <summary>The kit being edited, or null when the instrument is not one.</summary>
    private DrumKitViewModel? Kit => _designer.Editor?.Kit;

    /// <summary>
    /// A key went down or came up somewhere.
    /// </summary>
    /// <remarks>
    /// On whichever thread the note arrived on, which for the hardware is not the drawing one,
    /// so what it moves is moved over there.
    /// </remarks>
    private void Moved(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess()) Read();
        else Dispatcher.UIThread.Post(Read);
    }

    /// <summary>
    /// Reads the monitor, and moves the keyboard if a key is down that it cannot show.
    /// </summary>
    /// <remarks>
    /// Only the differences are written: this runs on every half of every key, and a collection
    /// that emptied and refilled itself would redraw a keyboard on which one key had changed.
    ///
    /// A key arriving from outside the octaves on show moves them, because a keyboard that cannot
    /// show the key it is lighting is showing nothing.
    /// </remarks>
    private void Read()
    {
        var down = _keys.Down;

        bool moved = false;

        for (int at = _lit.Count - 1; at >= 0; at--)
        {
            if (down.Contains(_lit[at])) continue;

            _lit.RemoveAt(at);
            moved = true;
        }

        foreach (int semitone in down)
        {
            if (_lit.Contains(semitone)) continue;

            _lit.Add(semitone);
            moved = true;

            Reveal(semitone);
        }

        if (moved) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves the octaves on show if a key landed outside them.</summary>
    private void Reveal(int semitone)
    {
        var note = new Note(semitone);
        if (!note.IsPlayable) return;

        Octave = _keyboard.Reveal(note, Octave);
    }

    /// <summary>
    /// Listens to whichever kit is in front of it now.
    /// </summary>
    /// <remarks>
    /// Asked on the way past rather than wired up once, because the instrument being edited
    /// changes under this and the kit changes with it. Picking a different pad is a change to
    /// the kit and nothing else says so.
    /// </remarks>
    private void Follow()
    {
        var kit = Kit;

        if (ReferenceEquals(kit, _watching)) return;

        if (_watching != null) _watching.PropertyChanged -= Moved;

        _watching = kit;

        if (_watching != null) _watching.PropertyChanged += Moved;
    }

    /// <summary>The kit said something moved, which is a pad picked or a sound put on one.</summary>
    /// <remarks>
    /// Every property of the kit, not one in particular: what a keyboard draws from a kit is which
    /// keys have sounds and which pad is in hand, and both are cheap enough to read again.
    /// </remarks>
    private void Moved(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
