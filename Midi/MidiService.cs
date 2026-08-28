using System;
using System.Collections.Generic;
using System.Linq;
using Commons.Music.Midi;

using JingleBox2.Diagnostics;

namespace JingleBox2.Midi;

public sealed class MidiService : IMidiService
{
    #pragma warning disable CS0618
    private readonly IMidiAccess? _access;
    #pragma warning restore CS0618

    // Keyed by the same display name the device list shows, so bindings and open ports match up.
    private readonly Dictionary<string, OpenPort> _ports = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public event EventHandler<MidiMessage>? MessageReceived;

    public MidiService()
    {
        try
        {
            _access = MidiAccessManager.Default; // IMidiAccess (obsolete warning suppressed via csproj)
        }
        catch
        {
            _access = null;
        }
    }

    public IReadOnlyList<string> GetInputDevices()
    {
        if (_access is null) return Array.Empty<string>();

        try
        {
            return _access.Inputs
                .Select(DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> OpenDevices
    {
        get
        {
            lock (_lock) return _ports.Keys.ToList();
        }
    }

    public bool Open(string deviceIdOrName)
    {
        if (_access is null) return false;
        if (string.IsNullOrWhiteSpace(deviceIdOrName)) return false;

        IMidiPortDetails? port;
        try
        {
            port = _access.Inputs.FirstOrDefault(p =>
                SameName(p.Id, deviceIdOrName) || SameName(p.Name, deviceIdOrName));
        }
        catch
        {
            return false;
        }

        if (port is null)
        {
            Log.Write(LogArea.Midi, () =>
                "port: '" + deviceIdOrName + "' is not among the inputs this system has, which are: "
                + Inputs());

            return false;
        }

        string name = DisplayName(port);

        lock (_lock)
        {
            if (_ports.ContainsKey(name)) return true;
        }

        IMidiInput input;
        try
        {
            input = _access.OpenInputAsync(port.Id).GetAwaiter().GetResult();
        }
        catch (Exception opening)
        {
            Log.Write(LogArea.Midi, () => "port: '" + name + "' would not open: " + opening.Message);
            return false;
        }

        // The handler closes over the name: that is the only place the port identity is still
        // known by the time a message arrives.
        EventHandler<MidiReceivedEventArgs> handler = (_, e) => OnMessageReceived(name, e);

        lock (_lock)
        {
            if (_ports.ContainsKey(name))
            {
                TryDispose(input);
                return true;
            }

            _ports[name] = new OpenPort(input, handler);
        }

        input.MessageReceived += handler;

        Log.Write(LogArea.Midi, () => "port: '" + name + "' is open and listening");

        return true;
    }

    /// <summary>
    /// The outputs that have been opened, by the name they were asked for.
    /// </summary>
    /// <remarks>
    /// Kept open. A device with a screen is written to on every turn of a knob, and opening a
    /// port per message would be absurd.
    /// </remarks>
    private readonly Dictionary<string, IMidiOutput> _outputs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sends bytes to a controller. Opens its output the first time, and keeps it.
    /// </summary>
    /// <remarks>
    /// A device with no output, or one that will not open, is answered with false rather than
    /// an exception: writing to a screen is a courtesy, and a controller without one is the
    /// ordinary case rather than a fault.
    /// </remarks>
    public bool Send(string deviceIdOrName, byte[] bytes)
    {
        if (_access is null || bytes is null || bytes.Length == 0) return false;
        if (string.IsNullOrWhiteSpace(deviceIdOrName)) return false;

        string name = deviceIdOrName.Trim();

        IMidiOutput? output;

        lock (_lock) _outputs.TryGetValue(name, out output);

        if (output is null)
        {
            output = OpenOutput(name);

            if (output is null) return false;
        }

        try
        {
            output.Send(bytes, 0, bytes.Length, 0);

            return true;
        }
        catch (Exception sending)
        {
            Log.Write(LogArea.Midi, () => "port: could not write to '" + name + "': " + sending.Message);

            // Dropped rather than kept, so the next write opens it again: a device unplugged
            // mid-session leaves a handle that will never work.
            lock (_lock) _outputs.Remove(name);

            return false;
        }
    }

    private IMidiOutput? OpenOutput(string name)
    {
        IMidiPortDetails? port;

        try
        {
            port = _access!.Outputs.FirstOrDefault(p => SameName(p.Id, name) || SameName(p.Name, name));
        }
        catch (Exception)
        {
            return null;
        }

        if (port is null)
        {
            Log.Write(LogArea.Midi, () => "port: '" + name + "' has no output to write to");
            return null;
        }

        IMidiOutput opened;

        try
        {
            opened = _access.OpenOutputAsync(port.Id).GetAwaiter().GetResult();
        }
        catch (Exception opening)
        {
            Log.Write(LogArea.Midi, () => "port: the output of '" + name + "' would not open: " + opening.Message);
            return null;
        }

        lock (_lock)
        {
            if (_outputs.TryGetValue(name, out var already))
            {
                TryDispose(opened);
                return already;
            }

            _outputs[name] = opened;
        }

        Log.Write(LogArea.Midi, () => "port: '" + name + "' is open to write to");

        return opened;
    }

    /// <summary>Every input this system is offering, for a log that has to say why one was missed.</summary>
    private string Inputs()
    {
        try
        {
            var names = _access?.Inputs.Select(p => "'" + DisplayName(p) + "'").ToList();

            return names is { Count: > 0 } ? string.Join(", ", names) : "none at all";
        }
        catch (Exception)
        {
            return "unreadable";
        }
    }

    public void Close(string deviceIdOrName)
    {
        if (string.IsNullOrWhiteSpace(deviceIdOrName)) return;

        OpenPort? port;
        lock (_lock)
        {
            if (!_ports.Remove(deviceIdOrName.Trim(), out port)) return;
        }

        Release(port);
    }

    public void CloseAll()
    {
        List<OpenPort> ports;
        lock (_lock)
        {
            ports = _ports.Values.ToList();
            _ports.Clear();
        }

        foreach (var port in ports)
            Release(port);
    }

    public void Dispose() => CloseAll();

    private static void Release(OpenPort port)
    {
        try
        {
            port.Input.MessageReceived -= port.Handler;
        }
        catch
        {
            // A device pulled out mid-session can throw on the way down; it is going away anyway.
        }

        TryDispose(port.Input);
    }

    private static void TryDispose(IDisposable port)
    {
        try
        {
            port.Dispose();
        }
        catch
        {
            // Same as above.
        }
    }

    /// <summary>
    /// What a port is called, as a name and not as whatever the driver padded it out to.
    /// </summary>
    /// <remarks>
    /// ALSA pads port names to a fixed width, so the same device is "MPD218 Port A" to one part
    /// of the system and "MPD218 Port A   " to another. Everything that stores a name trims it,
    /// so a name kept untrimmed here matches nothing the moment it has been through the
    /// settings once: the binding is saved trimmed, the port answers padded, and the port is
    /// then never opened again. Nothing reports that, because a port that will not be found is
    /// not an error anywhere; it is simply a controller that has gone quiet.
    /// </remarks>
    private static string DisplayName(IMidiPortDetails port) =>
        (string.IsNullOrWhiteSpace(port.Name) ? port.Id : port.Name).Trim();

    /// <summary>True when those two name the same port, however either was padded.</summary>
    public static bool SameName(string? left, string? right) =>
        string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What one delivery from a port holds, which is one message or several.
    /// </summary>
    /// <remarks>
    /// Several is the case that was missed, and it is not a rare one: a hand coming off a chord
    /// sends three note offs at the same instant and the port hands them over together. Only the
    /// first was read, so two keys were left sounding and lit with nothing left able to stop
    /// them. Every press arrived on its own, a millisecond or two apart, which is why the fault
    /// looked like the releases were not being sent at all.
    ///
    /// A message that takes none of the buffer is the system exclusive gatherer handing a byte
    /// back to be read as the start of something else. It is read again once, when nothing is
    /// being gathered; refusing twice would be a loop, so it stops.
    /// </remarks>
    private void OnMessageReceived(string device, MidiReceivedEventArgs e)
    {
        if (e.Data == null) return;

        int at = e.Start;
        int end = Math.Min(e.Start + e.Length, e.Data.Length);
        bool again = false;

        while (at < end)
        {
            var one = Read(device, e.Data, at, end - at, out int used);

            if (used <= 0)
            {
                if (again) return;

                again = true;
                continue;
            }

            again = false;

            Delivered(device, one, e.Data, at, used);

            at += used;
        }
    }

    /// <summary>One message read out of a delivery, said out loud or accounted for.</summary>
    private void Delivered(string device, MidiMessage? msg, byte[] data, int start, int length)
    {

        // The first hop of all, and only for what is thrown away here. A message that is
        // understood goes on to say for itself what it did; one dropped at the wire is silent
        // everywhere else, and a controller sending nothing looks exactly like a controller
        // whose messages we do not know how to read. Telling those two apart is what found
        // running status, where every message after the first arrived two bytes long and
        // vanished without a word.
        if (msg is null)
        {
            // The clock and active sensing arrive dozens of times a second on any device with a
            // sequencer in it, and a piece of a system exclusive message arrives whenever one is
            // long. Reporting either as unread would drown the very lines this log is kept for.
            if (!Chatter(data, start, length))
                Log.Write(LogArea.Midi, () =>
                    "port: '" + device + "' sent " + Bytes(data, start, length)
                    + " which is not a kind read here, so it is dropped");

            return;
        }

        // Rare, and the only kind worth printing whole. It is also how a device's identity gets
        // into a controller file: plug it in, ask, and read the answer out of the log.
        if (msg.Type == MidiMessageType.SystemExclusive)
            Log.Write(LogArea.Midi, () =>
                "port: '" + device + "' sent " + Said(msg.Bytes) + ": "
                + Bytes(msg.Bytes, 0, msg.Bytes?.Length ?? 0));

        MessageReceived?.Invoke(this, msg);
    }

    /// <summary>
    /// Whether those bytes are something dropped on purpose rather than something not understood.
    /// </summary>
    /// <remarks>
    /// Telling those two apart is the whole point of the line this guards. A message nobody
    /// reads is worth a line; the clock is not, at twenty four of them a beat.
    /// </remarks>
    private bool Chatter(byte[]? data, int start, int length)
    {
        if (data is null || length <= 0 || start < 0 || start >= data.Length) return true;

        byte first = data[start];

        // Clock, undefined, active sensing, reset.
        if (first is 0xF8 or 0xF9 or 0xFD or 0xFE or 0xFF) return true;

        // A piece of a system exclusive message that has not finished arriving.
        return first < 0x80 || first == 0xF0;
    }

    /// <summary>What a system exclusive message is, in words, for the log.</summary>
    /// <remarks>
    /// Three of them are worth naming and the rest are worth their manufacturer. The identity
    /// reply is the useful one: it is the only name a device has that is the same on every
    /// operating system, and it is what a controller file's identity field is for.
    /// </remarks>
    private static string Said(byte[]? sysex)
    {
        if (sysex is null || sysex.Length < 4) return "a system exclusive message";

        // F0 7F <device> 06 <command> F7
        if (sysex[1] == 0x7F && sysex.Length > 4 && sysex[3] == 0x06)
            return "MIDI Machine Control, command " + sysex[4].ToString("X2");

        // F0 7E <device> 06 01 F7 asks; 06 02 answers.
        if (sysex[1] == 0x7E && sysex.Length > 4 && sysex[3] == 0x06)
        {
            if (sysex[4] == 0x01) return "an identity request";
            if (sysex[4] == 0x02) return "an identity reply, which is what a profile's identity field wants";
        }

        return "a system exclusive message from manufacturer " + sysex[1].ToString("X2");
    }

    /// <summary>The raw bytes, for a log that has to say what really came off the wire.</summary>
    private static string Bytes(byte[]? data, int start, int length)
    {
        if (data is null || length <= 0) return "nothing";

        var said = new System.Text.StringBuilder();

        for (int at = start; at < start + length && at < data.Length; at++)
        {
            if (said.Length > 0) said.Append(' ');
            said.Append(data[at].ToString("X2"));
        }

        return said.Length == 0 ? "nothing" : said.ToString();
    }

    /// <summary>
    /// The status byte each device last sent, so a message that leaves it out can still be read.
    /// </summary>
    /// <remarks>
    /// Running status: a device sending a stream of the same kind of message sends the status
    /// byte once and then only the data. It is not an oddity, it is in the specification and
    /// almost every controller does it while a knob is being turned, which is precisely the
    /// stream this application most needs to read. Kept per device, because two controllers
    /// send two streams and one's status byte says nothing about the other's.
    /// </remarks>
    private readonly Dictionary<string, byte> _running = new(StringComparer.Ordinal);

    /// <summary>The three realtime bytes that mean something here.</summary>
    private const byte Started = 0xFA;
    private const byte Continued = 0xFB;
    private const byte Stopped = 0xFC;

    /// <summary>
    /// A system exclusive message being collected, per device.
    /// </summary>
    /// <remarks>
    /// Every other message in MIDI is one, two or three bytes and arrives whole. This one is as
    /// long as its sender likes and may be handed over in pieces, so it is the only place here
    /// that has to remember anything between callbacks. Per device for the same reason running
    /// status is: two controllers are two streams.
    /// </remarks>
    private readonly Dictionary<string, List<byte>> _building = new(StringComparer.Ordinal);

    /// <summary>Far more than any of these is, so a broken stream cannot grow without end.</summary>
    private const int TooLong = 4096;

    private bool Building(string device)
    {
        lock (_lock) return _building.ContainsKey(device);
    }

    /// <summary>
    /// Adds what has arrived to what was already there, and answers once it is whole.
    /// </summary>
    /// <remarks>
    /// Only 0xF7 ends one of these. A realtime byte is allowed to appear inside one and is not
    /// part of it, which is in the specification and is exactly what a device sending clock
    /// does while it answers an identity request. Any other byte with the top bit set means the
    /// sender abandoned the message part way, which is what a cable being pulled looks like.
    /// </remarks>
    private MidiMessage? Gather(string device, byte[] data, int at, int end, out int used)
    {
        int from = at;

        // Everything left, unless it finds the end of the message inside it.
        used = end - from;

        lock (_lock)
        {
            // Whatever run of messages was going, this ended it.
            _running.Remove(device);

            if (!_building.TryGetValue(device, out var so)) _building[device] = so = new List<byte>(16);

            for (; at < end; at++)
            {
                byte b = data[at];

                // Realtime, threaded through the middle of it. Not part of the message.
                if (b >= 0xF8) continue;

                if (b >= 0x80 && b != 0xF7)
                {
                    so.Clear();

                    // A fresh one starting is the common way to see this, and it is not a
                    // fault: the piece before it was simply never finished. The byte is handed
                    // back rather than eaten, because it is the start of whatever comes next.
                    if (b != 0xF0) { _building.Remove(device); used = at - from; return null; }
                }

                so.Add(b);

                if (b == 0xF7)
                {
                    var whole = so.ToArray();
                    _building.Remove(device);

                    // The message ends here, and anything after it in the buffer is the next
                    // one's business.
                    used = at + 1 - from;

                    return new MidiMessage
                    {
                        Device = device, Type = MidiMessageType.SystemExclusive,
                        Channel = 0, Value = 0, Data = 0, IsOn = false, Bytes = whole
                    };
                }

                if (so.Count > TooLong) { _building.Remove(device); used = at + 1 - from; return null; }
            }
        }

        // More of it to come.
        return null;
    }

    /// <summary>
    /// One message off the wire, or nothing when those bytes are not one this host plays.
    /// </summary>
    /// <remarks>
    /// Public because it holds the running status state and is the one piece here with a rule
    /// subtle enough to be worth testing on its own, away from any hardware.
    /// </remarks>
    public MidiMessage? Read(string device, byte[] data, int start, int length) =>
        Read(device, data, start, length, out _);

    /// <summary>
    /// The same, saying how many of those bytes it took.
    /// </summary>
    /// <remarks>
    /// A buffer off the wire is not one message. It is whatever the port had ready, and a hand
    /// coming off a chord puts three note offs into it at one instant. Reading the first and
    /// dropping the rest is what left keys sounding and lit with nothing able to stop them:
    /// pressing a chord arrives as three separate deliveries a millisecond or two apart and is
    /// read whole, and letting go of one arrives as a single delivery of which only the first
    /// note came out.
    ///
    /// So whoever is reading walks the buffer, and this is what tells them where the next
    /// message starts.
    /// </remarks>
    public MidiMessage? Read(string device, byte[] data, int start, int length, out int used)
    {
        used = 0;

        if (data == null || length <= 0 || start < 0 || start >= data.Length) return null;

        int at = start;
        int end = Math.Min(start + length, data.Length);

        byte status;

        // The one message with no length, and so the one that can arrive in pieces.
        if (data[at] == 0xF0 || Building(device)) return Gather(device, data, at, end, out used);

        if (data[at] >= 0x80)
        {
            status = data[at];
            at++;

            // A real time byte can turn up in the middle of anything and changes nothing about
            // what was being sent. Three of them are the transport and are read; the rest are
            // the clock and active sensing, which arrive dozens of times a second and say
            // nothing this application acts on.
            if (status >= 0xF8)
            {
                used = 1;

                return status is Started or Continued or Stopped
                    ? new MidiMessage { Device = device, Type = MidiMessageType.Realtime, Channel = 0, Value = status, Data = 0, IsOn = false }
                    : null;
            }

            if (status >= 0xF0)
            {
                lock (_lock) _running.Remove(device);
                used = 1;
                return null;
            }

            lock (_lock) _running[device] = status;
        }
        else
        {
            // Data with no status in front of it: the last one this device sent still stands.
            // Without this, a knob being turned is a stream of two-byte messages that read as
            // nothing at all, and only the very first move of the very first knob is heard.
            lock (_lock)
            {
                // Nothing to read it against, so the byte is stepped over rather than read
                // again for ever.
                if (!_running.TryGetValue(device, out status)) { used = 1; return null; }
            }
        }

        int type = status & 0xF0;
        int channel = (status & 0x0F) + 1;

        // Not every kind carries two. A program change and a channel pressure carry one, and
        // reading them as two takes the next message's status byte for a value and then loses
        // that message. Nothing here wants either of them, but running status means a wrong
        // length is not a wrong message, it is every message after it.
        int wants = type is 0xC0 or 0xD0 ? 1 : 2;

        // Half a message. Nothing here splits one across two deliveries, so what is left is not
        // going to be completed by anything: it is taken and dropped rather than read again.
        if (at + wants > end) { used = end - start; return null; }

        byte d1 = data[at];
        byte d2 = wants > 1 ? data[at + 1] : (byte)0;

        used = at + wants - start;

        return type switch
        {
            0x90 => new MidiMessage { Device = device, Type = MidiMessageType.Note, Channel = channel, Value = d1, Data = d2, IsOn = d2 > 0 },
            0x80 => new MidiMessage { Device = device, Type = MidiMessageType.Note, Channel = channel, Value = d1, Data = d2, IsOn = false },
            0xB0 => new MidiMessage { Device = device, Type = MidiMessageType.ControlChange, Channel = channel, Value = d1, Data = d2, IsOn = d2 > 0 },

            // Least significant seven bits first, which is the other way round from how the
            // rest of MIDI does it and the easiest thing in this file to write backwards.
            0xE0 => new MidiMessage { Device = device, Type = MidiMessageType.PitchBend, Channel = channel, Value = 0, Data = (d2 << 7) | d1, IsOn = false },

            _ => null
        };
    }

    private sealed record OpenPort(IMidiInput Input, EventHandler<MidiReceivedEventArgs> Handler);
}
