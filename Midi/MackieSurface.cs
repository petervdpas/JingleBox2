using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics;

namespace JingleBox2.Midi;

/// <summary>
/// What a Mackie Control surface is told: fader positions, button lights, knob rings and the
/// words on its display.
/// </summary>
/// <remarks>
/// The other half of <see cref="MidiMackieRouter"/>, and the half that makes a desk feel
/// attached to the music rather than merely wired to it. A surface with nothing written to it is
/// a box of controls that happen to move things. A surface being written to has the track's name
/// under the fader, the fader standing where the level is, and the mute light on when the track
/// is muted, so it can be read across a room and touched without looking.
///
/// It is also what makes the reading half correct rather than defensible. A fader here lands on
/// the value instead of picking up, which is right only because the fader has already been driven
/// to where the parameter is. Driving it is this.
///
/// Nothing here asks whether the device is listening. A port with no output is answered by
/// <see cref="IMidiService.Send"/> with a quiet false, and a surface that is not there costs a
/// few bytes down a port nobody reads. Same arrangement as <see cref="ArturiaDisplay"/>, and for
/// the same reason.
///
/// Every message is compared with what was last sent and dropped if it would say the same thing
/// again. That is not tidiness: a display line is sixty two bytes, there are two of them, and a
/// mixer that changes for any reason would otherwise redraw the whole desk on every property
/// that moved.
///
/// The numbers are Mackie Control's, read off Ardour's implementation of the same protocol under
/// the same licence. Copyright 2006-2007 John Anderson, 2012-2015 Paul Davis. See the remarks on
/// <see cref="MidiMackieRouter"/>.
/// <code>
/// fader      pitch bend on the strip's channel, fourteen bits
/// light      note on, 0x7F lit and 0x00 dark, on the button's own note
/// ring       CC 0x30 plus the strip: mode in bits 4 and 5, centre in bit 6, position in 0 to 3
/// words      F0 00 00 66 14 12 &lt;offset&gt; &lt;fifty six characters&gt; F7, second line at 0x38
/// </code>
/// </remarks>
public sealed class MackieSurface
{
    /// <summary>What every message to one of these begins with. 0x14 is a Mackie Control.</summary>
    private static readonly byte[] Head = { 0xF0, 0x00, 0x00, 0x66, 0x14 };

    private const int Strips = 8;

    /// <summary>Characters one strip gets on one line. Seven, on every surface that speaks this.</summary>
    private const int Room = 7;

    /// <summary>Where the second line starts in the display's own character array.</summary>
    private const byte Second = 0x38;

    /// <summary>The note each row of lights sits on, plus the strip's number.</summary>
    private const int SoloFrom = 0x08;
    private const int MuteFrom = 0x10;

    /// <summary>The first controller of the ring of lights round a knob.</summary>
    private const int RingFrom = 0x30;

    /// <summary>Lit from the centre outward, which is what a pan wants.</summary>
    private const int FromCentre = 1;

    private readonly IMidiService _midi;
    private readonly IControlTargets _targets;
    private readonly Func<int> _tracks;
    private readonly Func<int, string> _names;

    /// <param name="names">What to call each track, for the words under its fader.</param>
    public MackieSurface(IMidiService midi, IControlTargets targets, Func<int> tracks, Func<int, string> names)
    {
        _midi = midi;
        _targets = targets;
        _tracks = tracks;
        _names = names;
    }

    /// <summary>
    /// Which port to write to, learned from what arrives rather than configured.
    /// </summary>
    /// <remarks>
    /// A surface speaks and listens on the same port, so the first message it sends is also the
    /// address to answer on. Nothing has to be ticked twice and nothing has to be guessed, and a
    /// device plugged into a different socket next week still works.
    /// </remarks>
    public string Device { get; set; } = "";

    /// <summary>Which track the leftmost strip is on. Set by the router when it banks.</summary>
    public int Bank { get; set; }

    private readonly int[] _faders = Fresh(Strips);
    private readonly int[] _rings = Fresh(Strips);
    private readonly int[] _mutes = Fresh(Strips);
    private readonly int[] _solos = Fresh(Strips);
    private readonly bool[] _hands = new bool[Strips];
    private readonly string[] _lines = { "", "" };

    private static int[] Fresh(int many)
    {
        var made = new int[many];

        for (int at = 0; at < many; at++) made[at] = int.MinValue;

        return made;
    }

    /// <summary>
    /// A hand on a fader, or off it.
    /// </summary>
    /// <remarks>
    /// The surface says so, and it is the only way to know. While a hand is on a motorised
    /// fader the motor must be left alone: driving it means fighting the hand, and the hand
    /// wins in a way that feels like the desk is broken. Let go and it is put back where the
    /// value actually is, which also corrects anything the fight lost.
    /// </remarks>
    public void Touched(int strip, bool down)
    {
        if (strip < 0 || strip >= Strips) return;

        _hands[strip] = down;

        if (!down) Fader(strip);
    }

    /// <summary>
    /// Where the hardware says a fader now is, so it is not told what it has just said.
    /// </summary>
    /// <remarks>
    /// The loop this closes: a hand moves the fader, the level follows, the level having changed
    /// asks for the fader to be moved to match, and the message goes back down the wire to the
    /// fader that is already there. Recording what arrived as though it had been sent breaks it
    /// at the first step, without any timing or any suppression window.
    /// </remarks>
    public void Heard(int strip, int position)
    {
        if (strip < 0 || strip >= Strips) return;

        _faders[strip] = position;
    }

    /// <summary>Everything, which is what a bank change and a new song both want.</summary>
    public void Draw()
    {
        if (Device.Length == 0) return;

        for (int strip = 0; strip < Strips; strip++)
        {
            Fader(strip);
            Ring(strip);
            Light(strip, MuteFrom, MixControl.Mute, _mutes);
            Light(strip, SoloFrom, MixControl.Solo, _solos);
        }

        Words();
    }

    /// <summary>Forgets what the surface is showing, for one that has been unplugged.</summary>
    public void Gone()
    {
        for (int strip = 0; strip < Strips; strip++)
        {
            _faders[strip] = int.MinValue;
            _rings[strip] = int.MinValue;
            _mutes[strip] = int.MinValue;
            _solos[strip] = int.MinValue;
            _hands[strip] = false;
        }

        _lines[0] = _lines[1] = "";
    }

    private void Fader(int strip)
    {
        // A hand is on it. Whatever the value says, this fader is where somebody is holding it.
        if (_hands[strip]) return;

        if (Aim(strip, MixControl.Volume) is not { } target) return;

        double part = Part(target);
        int position = (int)Math.Round(part * 16383.0);

        if (position == _faders[strip]) return;

        _faders[strip] = position;

        Send(new byte[] { (byte)(0xE0 | strip), (byte)(position & 0x7F), (byte)((position >> 7) & 0x7F) });
    }

    private void Ring(int strip)
    {
        if (Aim(strip, MixControl.Pan) is not { } target) return;

        double part = Part(target);

        // One to eleven round the ring, and the centre light when it is near enough the middle
        // for a hand to have meant the middle.
        int lit = (int)Math.Round(part * 10.0) + 1;
        int value = (FromCentre << 4) | (lit & 0x0F);

        if (part is > 0.48 and < 0.58) value |= 1 << 6;

        if (value == _rings[strip]) return;

        _rings[strip] = value;

        Send(new byte[] { 0xB0, (byte)(RingFrom + strip), (byte)value });
    }

    private void Light(int strip, int from, MixControl what, int[] was)
    {
        if (Aim(strip, what) is not { } target) return;

        int lit = target.Value >= 0.5 ? 0x7F : 0x00;

        if (lit == was[strip]) return;

        was[strip] = lit;

        Send(new byte[] { 0x90, (byte)(from + strip), (byte)lit });
    }

    /// <summary>
    /// The two lines under the faders: what each track is, and where its knob is.
    /// </summary>
    /// <remarks>
    /// The name above and the knob's reading below, which is the arrangement every surface
    /// speaking this was built around. The level is not printed because the fader is already
    /// showing it, in the one way a number cannot.
    /// </remarks>
    private void Words()
    {
        var top = new System.Text.StringBuilder(Strips * Room);
        var bottom = new System.Text.StringBuilder(Strips * Room);

        for (int strip = 0; strip < Strips; strip++)
        {
            bool there = Aim(strip, MixControl.Volume) is not null;

            top.Append(Fit(there ? _names(Bank + strip) : ""));
            bottom.Append(Fit(there && Aim(strip, MixControl.Pan) is { } pan ? pan.Reads(pan.Value) : ""));
        }

        Line(0x00, top.ToString(), 0);
        Line(Second, bottom.ToString(), 1);
    }

    private void Line(byte at, string said, int which)
    {
        if (said == _lines[which]) return;

        _lines[which] = said;

        var message = new List<byte>(Head) { 0x12, at };

        foreach (char c in said) message.Add(c is >= ' ' and <= '~' ? (byte)c : (byte)'?');

        message.Add(0xF7);

        Send(message.ToArray());
    }

    /// <summary>Seven characters, however long the name was.</summary>
    private static string Fit(string said)
    {
        said ??= "";

        if (said.Length >= Room) return said.Substring(0, Room);

        return said.PadRight(Room);
    }

    /// <summary>Where a target stands in its own range, nought to one.</summary>
    private static double Part(IControlTarget target)
    {
        double span = target.Max - target.Min;

        return span <= 0 ? 0 : Math.Clamp((target.Value - target.Min) / span, 0, 1);
    }

    private IControlTarget? Aim(int strip, MixControl what) =>
        Bank + strip >= _tracks()
            ? null
            : _targets.Find(new ControlMapping
            {
                Kind = ControlKind.Mix,
                Mix = what,
                Scope = ControlScope.Fixed,
                Track = Bank + strip
            });

    private void Send(byte[] bytes)
    {
        if (Device.Length == 0) return;

        if (!_midi.Send(Device, bytes))
        {
            // The port has gone. Forget what it was showing, because it is not showing it any
            // more, and the guard above would otherwise refuse to send the very message that
            // would put it back.
            Gone();

            Log.Write(LogArea.Midi, () => "mackie: '" + Device + "' will not take a message, so what it shows is forgotten");
        }
    }
}
