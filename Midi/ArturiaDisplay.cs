using JingleBox2.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JingleBox2.Midi;

/// <summary>
/// Writes to the screen on a controller that has one.
/// </summary>
/// <remarks>
/// Arturia's own, and not a standard. Mackie Control can write text to a device's display and
/// this device is deaf to it: that message was written for a two line character panel in the
/// nineties and this is a colour screen the manufacturer drives their own way. Tried and
/// recorded in <c>docs/hardware-integration.md</c>, along with everything else that got nothing.
///
/// The shape came from https://gist.github.com/Janiczek/04a87c2534b9d1435a1d8159c742d260,
/// reverse engineered from what Arturia's own software sends:
/// <code>
/// F0 00 20 6B 7F 42 02 02 40 6A 21 F7                  wake the screen up, once
/// F0 00 20 6B 7F 42 04 02 60 01 &lt;first&gt; 00 02 &lt;second&gt; F7    two lines of text
/// F0 00 20 6B 7F 42 04 02 60 1F &lt;kind&gt; &lt;hide&gt; &lt;value&gt; 00 00 01 &lt;first&gt; 00 02 &lt;second&gt; F7
/// </code>
///
/// The third is the one worth having: it draws the value as well as the words, so a knob being
/// turned reads as what it is and where it is at the same time.
///
/// Nothing here asks whether the device is listening. A controller with no screen, or one whose
/// output will not open, is answered by <see cref="IMidiService.Send"/> with a quiet false, and
/// writing to a screen that is not there costs a few bytes down a port nobody is reading.
/// </remarks>
public sealed class ArturiaDisplay
{
    /// <summary>What every message to one of these begins with.</summary>
    private static readonly byte[] Head = { 0xF0, 0x00, 0x20, 0x6B, 0x7F, 0x42 };

    /// <summary>
    /// Sent once before the screen will take anything.
    /// </summary>
    /// <remarks>
    /// Not a wake, whatever it looks like. Arturia's settings protocol writes one device option
    /// as <c>F0 00 20 6B 7F 42 02 &lt;preset&gt; &lt;param&gt; &lt;control&gt; &lt;value&gt; F7</c>, and this is exactly
    /// that: preset 02, param 40, control 6A, value 21. Something is being switched on rather
    /// than roused, which is the likeliest reason the device stops speaking Mackie Control once
    /// it has been sent. The settings protocol is documented by https://github.com/soyersoyer/sysex-controls;
    /// see docs/hardware-integration.md.
    /// </remarks>
    private static readonly byte[] Wake = { 0xF0, 0x00, 0x20, 0x6B, 0x7F, 0x42, 0x02, 0x02, 0x40, 0x6A, 0x21, 0xF7 };

    /// <summary>What the value bar is drawn as.</summary>
    /// <remarks>
    /// The device's own picture of the control, so a knob's reading appears as a ring and a
    /// fader's as a bar. It is about the thing on the screen and not about the thing under your
    /// hand: a mixer level pointed at by an encoder is still drawn as a fader.
    /// </remarks>
    public enum Kind
    {
        /// <summary>A ring, for a parameter on a machine.</summary>
        Knob = 0x03,

        /// <summary>A bar, for anything on a mixer strip.</summary>
        Fader = 0x04,

        /// <summary>A pad, for a button pointed at an action.</summary>
        Pad = 0x05
    }

    /// <summary>How many characters a line will take before it is cut.</summary>
    private const int Room = 16;

    private readonly IMidiService _midi;

    /// <summary>The controllers to greet, asked each time since what is plugged in changes.</summary>
    private readonly Func<IEnumerable<string>>? _devices;

    /// <summary>Which devices have had <see cref="Wake"/> sent to them.</summary>
    /// <remarks>
    /// Forgotten again the moment a write fails, so a device unplugged and put back is woken
    /// afresh rather than being written to for ever with nothing appearing.
    /// </remarks>
    private readonly HashSet<string> _woken = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="midi">
    /// Where the messages go out. An output is opened on the device's own name when one is
    /// wanted, so a controller with no output costs nothing here.
    /// </param>
    /// <param name="devices">
    /// The controllers worth trying, for the text the screen shows at rest. Asked each time
    /// rather than held, because what is plugged in changes.
    /// </param>
    /// <remarks>
    /// Every one of them is written to, without asking which have screens. One with no output
    /// is answered with a quiet false, and one with an output and no screen receives a system
    /// exclusive message addressed to a manufacturer it is not, which it ignores. Both cost a
    /// few bytes down a port nobody is reading.
    /// </remarks>
    public ArturiaDisplay(IMidiService midi, Func<IEnumerable<string>>? devices = null)
    {
        _midi = midi;
        _devices = devices;
    }

    /// <summary>
    /// What the screen says when nothing else is happening.
    /// </summary>
    /// <remarks>
    /// The device shows the name of whichever DAW it was told about until somebody writes to
    /// it, and a knob's reading goes back to whatever was there before. So this is set once and
    /// everything else lands on top of it: turn a knob and the reading appears, take your hand
    /// off and the screen comes back here.
    /// </remarks>
    private string _first = "";

    /// <summary>The second line of the same.</summary>
    private string _second = "";

    /// <summary>
    /// Sets what the screen says at rest, and puts it there now.
    /// </summary>
    /// <remarks>
    /// Sent to every device that has been woken, and remembered for any that is woken later, so
    /// a controller plugged in halfway through gets the same greeting as one that was there
    /// from the start.
    ///
    /// Everything plugged in is written to, not only what has been woken. At the moment this is
    /// first called nothing has been woken at all, which is the whole of why the greeting never
    /// appeared: it was being said to an empty room.
    /// </remarks>
    public void Standing(string first, string second)
    {
        _first = first ?? "";
        _second = second ?? "";

        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        lock (_woken) foreach (string one in _woken) wanted.Add(one);

        try
        {
            foreach (string one in _devices?.Invoke() ?? Array.Empty<string>()) wanted.Add(one);
        }
        catch (Exception)
        {
        }

        foreach (string device in wanted) Say(device, _first, _second);
    }

    /// <summary>Two lines of words, and nothing else.</summary>
    public void Say(string device, string first, string second)
    {
        var message = new List<byte>(Head);

        message.AddRange(new byte[] { 0x04, 0x02, 0x60, 0x01 });
        message.AddRange(Text(first));
        message.AddRange(new byte[] { 0x00, 0x02 });
        message.AddRange(Text(second));
        message.Add(0xF7);

        Write(device, message.ToArray());
    }

    /// <summary>
    /// A control being moved: what it is, what it reads, and where it is in its range.
    /// </summary>
    /// <param name="device">The port to write to, named as the operating system names it.</param>
    /// <param name="kind">
    /// Which picture the reading is drawn in. About the thing on the screen rather than the
    /// thing under your hand: a mixer level driven by an encoder is still drawn as a fader.
    /// </param>
    /// <param name="fraction">Nought to one, which the screen draws as the bar.</param>
    /// <param name="what">The parameter's name, on the first line and cut at sixteen characters.</param>
    /// <param name="reads">What it now says, on the second line and cut the same way.</param>
    /// <param name="hide">
    /// True to have the screen go back to what it was showing after a moment, which is what a
    /// knob wants: the reading matters while your hand is on it and not afterwards.
    /// </param>
    public void Moved(string device, Kind kind, double fraction, string what, string reads, bool hide = true)
    {
        byte value = (byte)Math.Clamp((int)Math.Round(fraction * 127), 0, 127);

        var message = new List<byte>(Head);

        message.AddRange(new byte[] { 0x04, 0x02, 0x60, 0x1F, (byte)kind, (byte)(hide ? 0x02 : 0x00), value, 0x00, 0x00, 0x01 });
        message.AddRange(Text(what));
        message.AddRange(new byte[] { 0x00, 0x02 });
        message.AddRange(Text(reads));
        message.Add(0xF7);

        Write(device, message.ToArray());
    }

    /// <summary>
    /// A line as the screen will take it: plain ASCII, and not too much of it.
    /// </summary>
    /// <remarks>
    /// Anything above a hundred and twenty seven cannot go inside a system exclusive message at
    /// all, so a machine named with an accent would end the message early and the screen would
    /// show whatever the fragment happened to be. Replaced rather than refused, because a name
    /// is the user's and a screen is not the place to be strict about it.
    /// </remarks>
    private static byte[] Text(string said)
    {
        said ??= "";

        var bytes = new List<byte>(Room);

        foreach (char c in said)
        {
            if (bytes.Count >= Room) break;

            bytes.Add(c is >= ' ' and <= '~' ? (byte)c : (byte)'?');
        }

        return bytes.ToArray();
    }

    /// <summary>What was last sent to each device, so the same picture is not sent twice.</summary>
    /// <remarks>
    /// A hand on a knob is a hundred messages a second and each one redraws the screen, which is
    /// fine while the reading is changing because every one of them is a different picture. It
    /// stops being fine the moment the picture is the same: a control that has not picked up yet
    /// draws the value it is reaching for, which does not move, so a slow sweep would be several
    /// hundred identical system exclusive messages sent down the same port the knob's own
    /// messages are arriving on.
    /// </remarks>
    private readonly Dictionary<string, byte[]> _shown = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sends it, waking the screen the first time this device is written to.
    /// </summary>
    /// <remarks>
    /// A device with no output, or one that will not open, is forgotten again straight away so
    /// that plugging it in later still wakes it. The standing text goes up as soon as the screen
    /// wakes, so it stops showing the name of whatever DAW the device was told about and a knob's
    /// reading has somewhere of ours to fall back to.
    ///
    /// A device unplugged and put back has forgotten it was ever woken while this still thinks it
    /// was. The failed write is the moment to forget: the next one wakes it again, one message is
    /// lost to the replug, and nobody notices. What it was showing is forgotten with it, because
    /// it is not showing it any more, and without that the guard at the top would refuse to send
    /// the very message that would put it back on the grounds that the screen already says so.
    /// </remarks>
    private void Write(string device, byte[] message)
    {
        if (string.IsNullOrWhiteSpace(device)) return;

        lock (_shown)
        {
            if (_shown.TryGetValue(device, out var was) && was.AsSpan().SequenceEqual(message)) return;

            _shown[device] = message;
        }

        bool first;

        lock (_woken) first = _woken.Add(device);

        if (first)
        {
            if (!_midi.Send(device, Wake))
            {
                lock (_woken) _woken.Remove(device);

                return;
            }

            Log.Write(LogArea.Midi, () => "screen: woke the display on '" + device + "'");

            if (_first.Length > 0 || _second.Length > 0) Say(device, _first, _second);
        }

        if (!_midi.Send(device, message))
        {
            lock (_woken) _woken.Remove(device);

            lock (_shown) _shown.Remove(device);
        }
    }

    /// <summary>Forgets that a device was woken, for one that has gone.</summary>
    public void Gone(string device)
    {
        lock (_woken) _woken.Remove(device);
        lock (_shown) _shown.Remove(device);
    }
}
