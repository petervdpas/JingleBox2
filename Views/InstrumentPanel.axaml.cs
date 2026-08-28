using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JingleBox2.Views;

/// <summary>
/// The instrument designer: everything about one instrument's sound, and nothing about which
/// instrument it is.
/// </summary>
/// <remarks>
/// Its own control rather than a column of the INSTRUMENTS tab, because the same designer is
/// wanted in two places: on that tab, against whatever the rack has picked, and in a window
/// of its own, against the instrument a track is playing. A machine's front panel does not care
/// which rack it is standing in.
/// </remarks>
public partial class InstrumentPanel : UserControl
{
    /// <summary>
    /// Builds the panel and takes on everything a machine's own drawing cannot do for itself.
    /// </summary>
    /// <remarks>
    /// Everything on a machine is a setting except the few things that are not, and those are
    /// what these subscriptions are. A button that clears a pad or loads a folder of samples
    /// onto a kit goes to the same handlers the hand written panels use, so there is one way of
    /// doing each and not two. The shelf of takes is the application's, and a control drawn from
    /// a description knows nothing about where recordings are kept. Laying out a controller is
    /// the third: the panel says what the pointer is resting on and what was pressed, and where
    /// a hardware knob comes from is none of a drawing's business.
    ///
    /// The panel is painted in the machine's own shades, so it is repainted when the machine
    /// changes under it and mixed again when the theme moves under both.
    ///
    /// <see cref="LinkKey"/>.Watch puts it in the tally of places worth entering the other mouse
    /// mode for, which is what makes Ctrl+Shift+M mean anything here.
    ///
    /// A hardware button that has been pointed at something comes out through
    /// <see cref="Midi.ControlActions"/>, and this panel does it if it is the machine that was
    /// pointed at. Listened to only while the panel is on screen, so a panel nobody can see
    /// answers nothing.
    /// </remarks>
    public InstrumentPanel()
    {
        InitializeComponent();

        MachineFace.ActionWanted += Asked;

        MachineFace.TakeWanted += PickTake;

        MachineFace.LinkWanted += Offer;
        MachineFace.LinkActionWanted += OfferAction;
        MachineFace.UnlinkWanted += Drop;

        DataContextChanged += (_, _) => { Watch(); ShowLinks(); };
        UI.ThemeManager.Changed += Later;

        LinkKey.Watch(this);

        AttachedToVisualTree += (_, _) =>
        {
            if (Midi.ControlLink.Current is { } link) link.Changed += ShowLinks;

            Midi.ControlActions.Current.Fired += Do;

            ShowLinks();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            UI.ThemeManager.Changed -= Later;

            if (Midi.ControlLink.Current is { } link) link.Changed -= ShowLinks;

            Midi.ControlActions.Current.Fired -= Do;
        };
    }

    /// <summary>
    /// Puts one of your recordings on whatever setting the panel named.
    /// </summary>
    /// <remarks>
    /// The same shelf, in the same dialog, as the take picker on the hand written panels: one
    /// place a recording comes from, however the panel that asked for it was drawn.
    ///
    /// Everything on the panel that was showing the old one has to hear about the change, and so
    /// does whatever is drawing the recording underneath, which is what the second announcement
    /// is for: setting the text alone moves the value and leaves the picture.
    /// </remarks>
    private async void PickTake(object? sender, string key)
    {
        if (Designer?.Editor is not { Values: { } values } editor) return;

        var take = await TakeDialog.PickAsync(editor.Takes);

        if (take == null || take.FilePath.Length == 0) return;

        values.SetText(key, take.FilePath);

        editor.SaidAgain();
    }

    /// <summary>Does what a button on a described panel asked for.</summary>
    /// <remarks>
    /// Matched against the names in <see cref="JingleBox2.Machines.MachineActions"/> rather than
    /// against anything worked out from the string, so every action in the app can be found by
    /// searching for the word that is in the machine's file. One this build has never heard of
    /// does nothing, which is what lets a machine written against a later version still open.
    /// </remarks>
    private void Asked(object? sender, string action)
    {
        switch (action)
        {
            case Machines.MachineActions.ClearPad:
                ClearPadSample_Click(this, new RoutedEventArgs());

                break;

            case Machines.MachineActions.LoadPads:
                ImportPads_Click(this, new RoutedEventArgs());

                break;

            case Machines.MachineActions.ClearZone:
                ClearZoneSample_Click(this, new RoutedEventArgs());

                break;

            case Machines.MachineActions.LoadZones:
                ImportZones_Click(this, new RoutedEventArgs());

                break;

            case Machines.MachineActions.AddZone:
                Designer?.Editor?.Zones?.AddCommand.Execute(null);

                break;

            case Machines.MachineActions.RemoveZone:
                Designer?.Editor?.Zones?.RemoveCommand.Execute(null);

                break;

            case Machines.MachineActions.SpreadZones:
                Designer?.Editor?.Zones?.SpreadCommand.Execute(null);

                break;

            case Machines.MachineActions.PresetPrevious:
                Step(-1);

                break;

            case Machines.MachineActions.PresetNext:
                Step(1);

                break;
        }
    }

    /// <summary>
    /// Moves along the shelf of presets, the way the picker's own arrows do.
    /// </summary>
    /// <remarks>
    /// Through the shelf rather than the picker, because the shelf is what a preset being picked
    /// really means and the control on the panel is one way of saying it. Stopping at the ends
    /// rather than coming round: a list of presets has a first and a last, and a button held
    /// down that wrapped would take you past the one you were looking for without a pause.
    /// </remarks>
    private void Step(int by)
    {
        if (MachineFace.Presets is not { } shelf) return;

        int wanted = Machines.PresetStep.Moved(shelf.Picked, shelf.Names.Count, by);

        if (wanted != shelf.Picked) shelf.Picked = wanted;
    }

    /// <summary>The designer whose editor is being watched, so it is let go of again.</summary>
    private System.ComponentModel.INotifyPropertyChanged? _watched;

    /// <summary>
    /// Offers what the pointer is resting on to whatever is holding a controller.
    /// </summary>
    /// <remarks>
    /// The machine and the key, and no track. A knob is pointed at Zampler's cutoff rather than
    /// at track three's, so the same link works on every track that plays a Zampler and follows
    /// you as you move between them. What names a track is the mixer, which is a different kind
    /// of mapping for a different reason: see <see cref="Midi.ControlScope"/>.
    /// </remarks>
    private void Offer(object? sender, string key)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link)
        {
            Diagnostics.Log.Write(Diagnostics.LogArea.Midi, () =>
                "panel: pointer on '" + key + "' but "
                + (Midi.ControlLink.Current is null ? "THERE IS NO LINK" : "the mode is off"));

            return;
        }

        if (Designer?.Editor is not { } editor)
        {
            Diagnostics.Log.Write(Diagnostics.LogArea.Midi, () =>
                "panel: pointer on '" + key + "' but THERE IS NO EDITOR behind the panel, so nothing is offered");

            return;
        }

        link.Offer(new Midi.ControlMapping
        {
            Kind = Midi.ControlKind.Instrument,
            Scope = Midi.ControlScope.Focused,
            Machine = editor.MachineId,
            Key = key,
            Name = editor.MachineName + " " + key
        }, InSong);
    }

    /// <summary>
    /// Whether this panel is a song's instrument or the machine itself on the rack.
    /// </summary>
    /// <remarks>
    /// The same control stands in two places and they mean different things. On the rack it is
    /// the machine: pointing a knob at its filter is a fact about your hardware and that
    /// machine, true in every song you open, so it belongs on the desk. On a track it is this
    /// song's instrument, and pointing a knob at its filter is about this piece of music, so it
    /// travels in the file.
    /// </remarks>
    private bool InSong => Designer is TrackInstrumentDesigner;

    /// <summary>
    /// Offers a button on the panel, which is a press rather than a value.
    /// </summary>
    /// <remarks>
    /// A knob points at a parameter, which lives on the instrument and can be written by
    /// anything. A button points at something to be done, and only a panel knows how to do it:
    /// see <see cref="Midi.ControlActions"/>.
    ///
    /// The pickup is a jump, because a press is a press: there is nothing to work out and
    /// nothing to pick up from.
    /// </remarks>
    private void OfferAction(object? sender, string action)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link) return;
        if (Designer?.Editor is not { } editor) return;

        link.Offer(new Midi.ControlMapping
        {
            Kind = Midi.ControlKind.Action,
            Scope = Midi.ControlScope.Focused,
            Machine = editor.MachineId,
            Key = action,
            Pickup = Midi.ControlPickup.Jump,
            Name = editor.MachineName + " " + action.Replace('_', ' ')
        }, InSong);
    }

    /// <summary>Does what a mapped hardware button asked for, if it asked this machine.</summary>
    /// <remarks>
    /// Handed to the drawing thread, because the message arrives on the MIDI thread and what
    /// these actions do is open dialogs and rebuild grids.
    /// </remarks>
    private void Do(string machine, string action)
    {
        if (Designer?.Editor is not { } editor) return;
        if (!string.Equals(machine, editor.MachineId, StringComparison.Ordinal)) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() => Asked(this, action));
    }

    /// <summary>Takes whatever is pointed at that control off it.</summary>
    private void Drop(object? sender, string key)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link) return;
        if (Designer?.Editor is not { } editor) return;

        link.Unlink(editor.MachineId, key);
    }

    /// <summary>Tells the panel what mode the pointer is in and what is already pointed at.</summary>
    private void ShowLinks()
    {
        var link = Midi.ControlLink.Current;

        MachineFace.Linking = link?.IsLinking ?? false;

        MachineFace.Linked = link is null || Designer?.Editor is not { } editor
            ? null
            : link.KeysOn(editor.MachineId);

        MachineFace.LinkedActions = link is null || Designer?.Editor is not { } showing
            ? null
            : link.ActionsOn(showing.MachineId);
    }

    /// <summary>
    /// Listens to whichever designer the panel has been given, and lets go of the one before.
    /// </summary>
    /// <remarks>
    /// Without the letting go the panel would be subscribed to every designer it had ever
    /// shown, and a machine picked on the rack would repaint the panel several times over.
    /// </remarks>
    private void Watch()
    {
        if (_watched != null) _watched.PropertyChanged -= OnDesignerChanged;

        _watched = DataContext as System.ComponentModel.INotifyPropertyChanged;

        if (_watched != null) _watched.PropertyChanged += OnDesignerChanged;

        Retint();
    }

    /// <summary>
    /// Repaints when the instrument behind the designer changes.
    /// </summary>
    /// <remarks>
    /// Watched rather than taken off the data context, because the rack keeps the same designer
    /// and swaps the instrument inside it: the panel is never given a new designer and would
    /// otherwise go on wearing the last machine's colours.
    /// </remarks>
    private void OnDesignerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IInstrumentDesigner.Editor)) Retint();
    }

    /// <summary>
    /// Repainted after the theme swap has settled rather than during it.
    /// </summary>
    /// <remarks>
    /// The shades are mixed against the theme's own colours, and read in the middle of the
    /// swap those are still the old theme's: the panel came out of a light theme still wearing
    /// light cards on a dark page.
    /// </remarks>
    private void Later() => Avalonia.Threading.Dispatcher.UIThread.Post(Retint);

    /// <summary>Paints the panel in the machine's own colours, mixed against the current theme.</summary>
    private void Retint() => MachineTint.Apply(this, Editor?.Theme);

    /// <summary>The instrument being worked on, or nothing when the designer has none.</summary>
    private InstrumentEditorViewModel? Editor => (DataContext as IInstrumentDesigner)?.Editor;

    /// <summary>
    /// What this panel is standing in: the rack, or a track's own window. See
    /// <see cref="InSong"/> for why the difference matters.
    /// </summary>
    private IInstrumentDesigner? Designer => DataContext as IInstrumentDesigner;

    /// <summary>Whatever window this panel is in, since that is where the keys arrive.</summary>
    private TopLevel? _keySource;

    /// <summary>
    /// The panel listens for the tracker's piano layout so it can be played where it stands.
    /// </summary>
    /// <remarks>
    /// The handler goes on the window rather than on this control: a key press only tunnels
    /// through the controls between the root and whatever has focus, and after clicking a knob
    /// or a combo box that route does not have to come past here.
    ///
    /// One window, one panel, so the INSTRUMENTS tab and an instrument's own window each get
    /// their own and neither hears the other's keys.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _keySource = TopLevel.GetTopLevel(this);
        _keySource?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _keySource?.AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Gives the window's keys back. Held on to nothing afterwards, since the next attach may
    /// be into a different window.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _keySource?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _keySource?.RemoveHandler(KeyUpEvent, OnKeyUp);
        _keySource = null;
    }

    /// <summary>
    /// A letter on the computer keyboard plays the machine, in the tracker's piano layout.
    /// </summary>
    /// <remarks>
    /// A panel on a tab nobody is looking at must not answer for the one they are, which is why
    /// the visibility is asked as well as whether there is anything to play. Typing a name is
    /// typing a name and not playing a tune, so a text box anywhere in the window takes the key
    /// instead, and anything with a modifier on it is somebody else's shortcut.
    ///
    /// Played through the keyboard's own set rather than sounded directly, so a key held down
    /// repeats nothing and the key on screen lights for exactly as long as the one under your
    /// finger is down.
    /// </remarks>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var designer = Designer;

        if (designer?.Editor == null || !IsEffectivelyVisible) return;

        if (e.Source is TextBox) return;
        if (_keySource?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (e.KeyModifiers != KeyModifiers.None) return;

        if (KeyboardNoteMap.NoteFor(e.Key.ToString(), designer.Octave) is not Note note) return;

        designer.MachineKeys.Play(note.Semitone);

        e.Handled = true;
    }

    /// <summary>
    /// A typed key let go, which is what puts its light out.
    /// </summary>
    /// <remarks>
    /// The other half of playing by typing. Without it a key lights until whatever it started
    /// stops sounding, which on a cymbal is four seconds after the hand came off.
    /// </remarks>
    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        var designer = Designer;

        if (designer?.Editor == null || !IsEffectivelyVisible) return;

        if (KeyboardNoteMap.NoteFor(e.Key.ToString(), designer.Octave) is not Note note) return;

        designer.MachineKeys.Let(note.Semitone);
    }

    /// <summary>
    /// One of your own takes, straight onto the pad in hand.
    /// </summary>
    /// <remarks>
    /// The picker is cleared afterwards so it reads as an action rather than a setting: what
    /// is on the pad is written under it, and a box still showing the last thing you put there
    /// would be claiming to be the pad's own.
    /// </remarks>
    private void PadRecording_Changed(object? sender, TakePickedEventArgs e) =>
        Designer?.Editor?.Kit?.Selected?.Take(e.Take.FilePath);

    /// <summary>One of your own takes, straight onto the zone in hand.</summary>
    private void ZoneRecording_Changed(object? sender, TakePickedEventArgs e) =>
        Designer?.Editor?.Zones?.Selected?.Take(e.Take.FilePath);

    /// <summary>
    /// Brings samples in from the disc and fills the pads with them.
    /// </summary>
    /// <remarks>
    /// Many at once, because a kit is a folder of hits rather than one file. They are copied
    /// into JingleBox on the way, so from here on the machine is playing its own.
    /// </remarks>
    private async void ImportPads_Click(object? sender, RoutedEventArgs e)
    {
        var editor = Designer?.Editor;
        if (editor?.Kit == null) return;

        var found = await AskFiles("Samples to load onto the pads");
        if (found.Count == 0) return;

        editor.Kit.Fill(editor.Import(found));
    }

    /// <summary>Brings samples in from the disc and builds the whole map from them.</summary>
    private async void ImportZones_Click(object? sender, RoutedEventArgs e)
    {
        var editor = Designer?.Editor;
        if (editor?.Zones == null) return;

        var found = await AskFiles("Samples to load onto the keyboard");
        if (found.Count == 0) return;

        editor.Zones.Fill(editor.Import(found));
    }

    /// <summary>
    /// Asks for any number of samples, sorted by name.
    /// </summary>
    /// <remarks>
    /// Sorted the way the folder would be read, since a set of samples is nearly always named
    /// so that it sorts, and that order is very often the order across the pads or up the
    /// keyboard. The picker belongs to the window, so it is opened here and only the answer
    /// goes to the view model.
    /// </remarks>
    private async System.Threading.Tasks.Task<IReadOnlyList<string>> AskFiles(string title)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return Array.Empty<string>();

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Samples")
                {
                    Patterns = RecordingImport.Kinds.Select(k => "*" + k).ToArray()
                }
            }
        });

        return picked
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Takes the recording off the zone in hand, leaving the zone where it is.</summary>
    private void ClearZoneSample_Click(object? sender, RoutedEventArgs e) =>
        Designer?.Editor?.Zones?.Selected?.Take(null);

    /// <summary>Takes the recording off the pad in hand, leaving the pad where it is.</summary>
    private void ClearPadSample_Click(object? sender, RoutedEventArgs e) =>
        Designer?.Editor?.Kit?.Selected?.Take(null);

    /// <summary>The plugin instrument this stands for, opened in the same window a chain uses.</summary>
    /// <remarks>
    /// The instrument is held in <see cref="_openEditor"/> so the same handler can be taken off
    /// again when another one is opened. A local function would make a new delegate every time
    /// and they would pile up, each closing a window that had already gone.
    /// </remarks>
    private void OpenPluginWindow_Click(object? sender, RoutedEventArgs e)
    {
        var editor = Editor;
        if (editor?.PluginPanel == null) return;

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        if (_openEditor != null) _openEditor.Closing -= CloseOpenEditor;

        _openEditor = editor;
        editor.Closing += CloseOpenEditor;

        PluginWindow.Show(editor, editor.PluginPanel, editor.PluginText, owner);
    }

    /// <summary>The instrument whose plugin window this designer opened, if any.</summary>
    private InstrumentEditorViewModel? _openEditor;

    /// <summary>
    /// Puts the plugin's window away when the instrument behind it is let go of, which is the
    /// one case a window would otherwise be left drawing into a plugin that has been disposed.
    /// </summary>
    private void CloseOpenEditor()
    {
        var editor = _openEditor;
        if (editor == null) return;

        editor.Closing -= CloseOpenEditor;
        _openEditor = null;

        PluginWindow.CloseFor(editor);
    }
}
