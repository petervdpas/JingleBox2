using JingleBox2.ViewModels;
using System;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <summary>
/// A kit, shown to a described panel as the grid of pads on its face.
/// </summary>
/// <remarks>
/// The kit already exists and is already being edited: <see cref="DrumKitViewModel"/> is what
/// the pads have always been bound to. This is the same kit answering the questions the panel
/// asks, which are fewer and simpler, because a panel drawn from a description knows nothing
/// about drums.
///
/// Nothing is copied. Two views of one kit that each held their own list would disagree the
/// first time a note was played, and what is being watched here moves on every note.
/// </remarks>
/// <param name="kit">The kit on the other side, which is the one the editor is already on.</param>
/// <param name="keys">
/// What is watching the notes going past, so a pad held down lights its key the way the same
/// note held on the drawn keyboard does. Left out, a pad still sounds and still lights itself,
/// and the keyboard stays dark: that is what a preview has, since there is no hand on it.
/// </param>
public sealed class KitPads(DrumKitViewModel kit, Midi.Interfaces.IMidiMonitor? keys = null) : IPanelPads
{
    /// <summary>Following a list of things and what each of them says.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISoundMachineWatch Watching = new SoundMachineWatch();

    /// <summary>
    /// Whether the kit is being watched yet.
    /// </summary>
    /// <remarks>
    /// The subscription is put on when the first listener arrives and never taken off, so this is
    /// a one-way latch rather than a count: a panel opened, shut and opened again would otherwise
    /// hang a second set of handlers on the same kit.
    /// </remarks>
    private bool _listening;

    /// <inheritdoc/>
    public int Count => kit.Pads.Count;

    /// <inheritdoc/>
    public string Cap(int at) => Pad(at)?.CapText ?? "";

    /// <inheritdoc/>
    public string Note(int at) => Pad(at)?.NoteText ?? "";

    /// <inheritdoc/>
    public bool Lit(int at) => Pad(at)?.IsLit ?? false;

    /// <inheritdoc/>
    public bool Filled(int at) => Pad(at)?.HasSound ?? false;

    /// <inheritdoc/>
    /// <remarks>
    /// Minus one when nothing is picked, which is what an empty kit and a freshly opened panel
    /// both look like. Every key in <see cref="KitValues"/> is about whichever pad this names,
    /// so a panel with nothing picked reads zeroes rather than the first pad's settings.
    /// </remarks>
    public int Picked
    {
        get => kit.Selected is { } one ? kit.Pads.IndexOf(one) : -1;
        set => kit.SelectAt(value);
    }

    /// <inheritdoc/>
    /// <remarks>Through the pad's own tap command, so a panel's press and a key press are one act.</remarks>
    public void Hit(int at) => Pad(at)?.TapCommand.Execute(null);

    /// <inheritdoc/>
    /// <remarks>
    /// Straight to the monitor rather than through the pad, because this is the light and not
    /// the sound: the monitor is what every drawn keyboard reads itself from, and telling it is
    /// the whole of what a key going down means here.
    /// </remarks>
    public void Held(int at)
    {
        if (Pad(at) is { } pad && pad.Pad.Note.IsPlayable) keys?.Pressed(pad.Pad.Note.Semitone);
    }

    /// <inheritdoc/>
    public void Let(int at)
    {
        if (Pad(at) is { } pad && pad.Pad.Note.IsPlayable) keys?.Released(pad.Pad.Note.Semitone);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Wired on the first listener rather than in the constructor, so a kit nothing is watching
    /// costs nothing. The pads are watched as well as the kit: a pad lighting is a change to the
    /// pad, and the kit says nothing when one does.
    /// </remarks>
    public event EventHandler? Changed
    {
        add
        {
            _changed += value;

            Listen();
        }
        remove => _changed -= value;
    }

    /// <summary>Everyone told when a pad lights, is picked, or is given a different recording.</summary>
    private EventHandler? _changed;

    /// <summary>
    /// Puts the subscription on, once.
    /// </summary>
    /// <remarks>
    /// A kit refilled from a chop is a new set of pads, which is why the list is watched as well
    /// as the pads in it: see <see cref="SoundMachineWatch"/>.
    /// </remarks>
    private void Listen()
    {
        if (_listening) return;

        _listening = true;

        Watching.Items<DrumPadViewModel>(
            kit, kit.Pads, () => kit.Pads, () => _changed?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>That pad, or nothing when the number is outside the kit.</summary>
    /// <remarks>
    /// A panel is drawn from a description that can name more pads than the kit has, and a stale
    /// number arriving while a kit is being refilled is ordinary rather than a fault, so every
    /// reader above holds against nothing rather than throwing.
    /// </remarks>
    private DrumPadViewModel? Pad(int at) => kit.Pads.ElementAtOrDefault(at);
}
