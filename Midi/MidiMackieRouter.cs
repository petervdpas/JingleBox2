using System;
using JingleBox2.Diagnostics;

namespace JingleBox2.Midi;

/// <summary>
/// A control surface speaking Mackie Control: its faders, its knobs and its strip buttons.
/// </summary>
/// <remarks>
/// The fifth router, and the same shape as the four before it: this one knows a protocol and
/// nothing about the application, and reaches the mixer through <see cref="IControlTargets"/>,
/// which is the same door a link somebody made by hand writes through. See
/// <see cref="MidiRouter"/> for pads, <see cref="MidiNoteRouter"/> for notes,
/// <see cref="MidiControlRouter"/> for knobs and <see cref="MidiTransportRouter"/> for the
/// transport, whose five buttons are deliberately not read here: they arrive on this same port
/// and that router already answers them, and answering twice would stop what the press started.
///
/// Why this is worth having at all: every control surface built in the last thirty years speaks
/// it. A device that does needs no file, no learning and no layout, because the protocol says
/// what each control is. That is the opposite of the rest of the MIDI in this application, where
/// a controller number says nothing about the thing that sent it and three messages have to be
/// watched to guess. Here there is nothing to guess, which is why none of the sensing machinery
/// appears below.
///
/// It is closed and it is documented anyway. Mackie never published it, but the same hardware
/// shipped as Emagic's Logic Control and Emagic did, and Ardour has carried a full implementation
/// under the GPL since 2006. The numbers here were read off
/// <c>libs/surfaces/mackie/device_info.cc</c> and <c>surface.cc</c> in Ardour, which is version 2
/// or later of the same licence this is under. Copyright 2006-2007 John Anderson, 2012-2015 Paul
/// Davis. Nothing was copied: the tables are facts about hardware, and the code around them is
/// written against a model that is not this one.
///
/// <code>
/// faders        pitch bend, channel 1 to 8 the strips, channel 9 the master
/// v-pots        CC 0x10 to 0x17, relative, sign in bit 6 and ticks in bits 0 to 5
/// jog           CC 0x3C, counted the same way
/// rec           notes 0x00 to 0x07      solo    notes 0x08 to 0x0F
/// mute          notes 0x10 to 0x17      select  notes 0x18 to 0x1F
/// v-pot press   notes 0x20 to 0x27      touch   notes 0x68 to 0x6F
/// banking       notes 0x2E and 0x2F by eight, 0x30 and 0x31 by one
/// </code>
/// </remarks>
public sealed class MidiMackieRouter
{
    /// <summary>How many strips a surface shows at once. Eight, on everything that speaks this.</summary>
    private const int Strips = 8;

    /// <summary>The first note of each row of strip buttons. The strip's number is added to it.</summary>
    private const int RecFrom = 0x00;
    private const int SoloFrom = 0x08;
    private const int MuteFrom = 0x10;
    private const int SelectFrom = 0x18;
    private const int PressFrom = 0x20;
    private const int TouchFrom = 0x68;

    /// <summary>Moving the eight strips along the tracks.</summary>
    private const int BankLeft = 0x2E;
    private const int BankRight = 0x2F;
    private const int ChannelLeft = 0x30;
    private const int ChannelRight = 0x31;

    /// <summary>The transport, which <see cref="MidiTransportRouter"/> answers and this does not.</summary>
    private const int TransportFrom = 0x5B;
    private const int TransportTo = 0x5F;

    /// <summary>The knobs above the strips, as continuous controllers.</summary>
    private const int PotFrom = 0x10;
    private const int PotTo = 0x17;

    /// <summary>The wheel, counted exactly as a knob is.</summary>
    private const int Jog = 0x3C;

    /// <summary>What a fader's fourteen bits count up to.</summary>
    private const double Travel = 16383.0;

    /// <summary>What a knob's ticks count up to, which is six bits.</summary>
    private const double Ticks = 63.0;

    private readonly IControlTargets _targets;
    private readonly Func<int> _tracks;
    private readonly MackieSurface? _surface;

    /// <param name="targets">
    /// Where a fader, a knob or a button lands: the mixer, through the same door a link written
    /// by hand goes through. A surface says what every control on it is, so nothing here has to
    /// be learned or pointed.
    /// </param>
    /// <param name="tracks">
    /// How many there are, so banking stops at the end instead of walking off it and leaving
    /// eight faders pointed at nothing with no clue as to why.
    /// </param>
    /// <param name="surface">
    /// The half that writes back, where there is one. Optional because the reading half is
    /// worth having on its own and because every test of this would otherwise need a port.
    /// </param>
    public MidiMackieRouter(IControlTargets targets, Func<int> tracks, MackieSurface? surface = null)
    {
        _targets = targets;
        _tracks = tracks;
        _surface = surface;
    }

    /// <summary>Which track the leftmost strip is on.</summary>
    public int Bank { get; private set; }

    /// <summary>
    /// Reads one message off a surface: a fader, a knob, or a button.
    /// </summary>
    /// <remarks>
    /// The port to write back on is learned here rather than configured. A surface speaks and
    /// listens on the same port, so the first thing it says is also the address to answer on:
    /// nothing has to be ticked twice, and a device moved to another socket still works.
    /// </remarks>
    public void Handle(MidiMessage? message)
    {
        if (message is null) return;

        if (_surface is not null && !string.Equals(_surface.Device, message.Device, StringComparison.Ordinal))
        {
            _surface.Device = message.Device;
            _surface.Bank = Bank;
            _surface.Gone();
            _surface.Draw();
        }

        switch (message.Type)
        {
            case MidiMessageType.PitchBend: Fader(message); return;
            case MidiMessageType.ControlChange: Turned(message); return;
            case MidiMessageType.Note: Pressed(message); return;
        }
    }

    /// <summary>
    /// A fader, which is a position and lands on it.
    /// </summary>
    /// <remarks>
    /// Absolute and immediate, where every other position-reporting control in this application
    /// is picked up. That is not an inconsistency: on a surface like this the fader is motorised
    /// and has already been driven to where the parameter is, so its position and the parameter's
    /// are the same thing and there is nothing to reconcile. Picking up would mean hunting for a
    /// value the fader is already sitting on.
    ///
    /// Which is only true because <see cref="MackieSurface"/> drives it there. Without the
    /// writing half this would be right in principle and wrong in the room: the first touch
    /// after opening a song would throw the level to wherever the fader was left standing.
    ///
    /// The ninth channel is the surface's master fader, which this has nothing to move. Named
    /// rather than ignored, so a fader that does nothing says why.
    ///
    /// What arrives is written down as though it had been sent, which is what stops the level,
    /// having changed, asking for this fader to be moved to where it already is.
    /// </remarks>
    private void Fader(MidiMessage message)
    {
        int strip = message.Channel - 1;

        if (strip == Strips)
        {
            Say(message, "the master fader, which this has nothing to move");
            return;
        }

        if (strip < 0 || strip >= Strips) return;

        if (Aim(strip, MixControl.Volume) is not { } target) return;

        double part = Math.Clamp(message.Data / Travel, 0, 1);

        _surface?.Heard(strip, message.Data);

        target.Set(target.Min + part * (target.Max - target.Min));

        Moved(message, target, strip);
    }

    /// <summary>
    /// A knob, which counts how far it moved and never says where it is.
    /// </summary>
    /// <remarks>
    /// Bit six is the direction and the six below it are how far, counted since the last message
    /// rather than since anything fixed. A device sending nought ticks means one: the encoders on
    /// some surfaces do that, and read literally the knob would be dead.
    /// </remarks>
    private void Turned(MidiMessage message)
    {
        if (message.Value == Jog)
        {
            Say(message, "the wheel, which this does nothing with yet");
            return;
        }

        if (message.Value < PotFrom || message.Value > PotTo) return;

        int strip = message.Value - PotFrom;

        double way = (message.Data & 0x40) == 0 ? 1 : -1;
        double ticks = message.Data & 0x3F;

        if (ticks == 0) ticks = 1;

        if (Aim(strip, MixControl.Pan) is not { } target) return;

        double step = way * (ticks / Ticks) * (target.Max - target.Min);

        target.Set(Math.Clamp(target.Value + step, target.Min, target.Max));

        Moved(message, target, strip);
    }

    /// <summary>
    /// A button, on the press and not the release.
    /// </summary>
    /// <remarks>
    /// With one exception: letting go of a fader is a message worth having and it is a note off,
    /// so the touch row is read before the guard. Everything else here is a press, since held is
    /// a note on at full velocity and let go is the same note at nothing, and acting on both
    /// would toggle twice and leave it as it was.
    ///
    /// The five transport notes are refused by name, because <see cref="MidiTransportRouter"/>
    /// already answers them and they arrive on this same port. That is the only place the two can
    /// overlap, and answering twice would stop what the press had started.
    ///
    /// Three rows are named and do nothing, because there is nothing here for them to do: a
    /// track is not armed one at a time in this application, selecting a strip is the pattern
    /// cursor's business rather than a surface's, and a knob press is a surface's own idea of
    /// reset.
    /// </remarks>
    private void Pressed(MidiMessage message)
    {
        int note = message.Value;

        if (Within(note, TouchFrom)) { _surface?.Touched(note - TouchFrom, message.IsOn); return; }

        if (!message.IsOn) return;

        if (note >= TransportFrom && note <= TransportTo) return;

        switch (note)
        {
            case BankLeft: Move(message, -Strips); return;
            case BankRight: Move(message, Strips); return;
            case ChannelLeft: Move(message, -1); return;
            case ChannelRight: Move(message, 1); return;
        }

        if (Switched(message, note, SoloFrom, MixControl.Solo, "solo")) return;
        if (Switched(message, note, MuteFrom, MixControl.Mute, "mute")) return;

        if (Within(note, RecFrom)) { Say(message, "record arm on strip " + (note - RecFrom + 1) + ", which this does nothing with"); return; }
        if (Within(note, SelectFrom)) { Say(message, "select on strip " + (note - SelectFrom + 1) + ", which this does nothing with yet"); return; }
        if (Within(note, PressFrom)) { Say(message, "a knob pressed on strip " + (note - PressFrom + 1) + ", which this does nothing with yet"); return; }

        Log.Write(LogArea.Midi, () =>
            "mackie: '" + message.Device + "' sent note " + note
            + ", which is a button this does not read");
    }

    /// <summary>
    /// One of the rows of eight, switched by pressing it.
    /// </summary>
    /// <remarks>
    /// Read and turned over rather than set from the button, because the button says it was
    /// pressed and nothing else: there is no on and off in a press.
    /// </remarks>
    private bool Switched(MidiMessage message, int note, int from, MixControl what, string called)
    {
        if (!Within(note, from)) return false;

        int strip = note - from;

        if (Aim(strip, what) is not { } target) return true;

        double now = target.Value >= 0.5 ? 0 : 1;

        target.Set(now);

        _surface?.Draw();

        Log.Write(LogArea.Midi, () =>
            "mackie: '" + message.Device + "' pressed " + called + " on strip " + (strip + 1)
            + ", which is track " + (Bank + strip + 1) + ", now " + (now >= 0.5 ? "on" : "off"));

        return true;
    }

    /// <summary>Whether that note is in the row of eight starting there.</summary>
    private static bool Within(int note, int from) => note >= from && note < from + Strips;

    /// <summary>Where a strip is pointed, which is its place plus wherever the bank has got to.</summary>
    private IControlTarget? Aim(int strip, MixControl what) =>
        _targets.Find(new ControlMapping
        {
            Kind = ControlKind.Mix,
            Mix = what,
            Scope = ControlScope.Fixed,
            Track = Bank + strip
        });

    /// <summary>
    /// Moves the eight strips along the tracks.
    /// </summary>
    /// <remarks>
    /// Clamped at both ends rather than wrapping. A desk that wraps round from the last track to
    /// the first is a desk you can get lost on, and the number of tracks is not printed anywhere
    /// on the hardware to count against.
    /// </remarks>
    private void Move(MidiMessage message, int by)
    {
        int tracks = Math.Max(0, _tracks());
        int most = Math.Max(0, tracks - Strips);
        int wanted = Math.Clamp(Bank + by, 0, most);

        if (wanted == Bank)
        {
            Say(message, by < 0 ? "left, and it is already at the first track"
                                : "right, and it is already at the last of them");
            return;
        }

        Bank = wanted;

        if (_surface is not null) { _surface.Bank = Bank; _surface.Draw(); }

        Log.Write(LogArea.Midi, () =>
            "mackie: '" + message.Device + "' moved the strips to tracks "
            + (Bank + 1) + " to " + Math.Min(tracks, Bank + Strips));
    }

    /// <summary>The line a fader or a knob earns: what moved, from which strip, and to what.</summary>
    private void Moved(MidiMessage message, IControlTarget target, int strip) =>
        Log.Write(LogArea.Midi, () =>
            "mackie: '" + message.Device + "' moved " + target.Name
            + " from strip " + (strip + 1) + " to " + target.Reads(target.Value));

    /// <summary>The line something named but unused earns, so it says why nothing happened.</summary>
    private static void Say(MidiMessage message, string what) =>
        Log.Write(LogArea.Midi, () => "mackie: '" + message.Device + "' sent " + what);
}
