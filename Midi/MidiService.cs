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

    private static void TryDispose(IMidiInput input)
    {
        try
        {
            input.Dispose();
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

    private void OnMessageReceived(string device, MidiReceivedEventArgs e)
    {
        var msg = Read(device, e.Data, e.Start, e.Length);

        // The first hop of all, and only for what is thrown away here. A message that is
        // understood goes on to say for itself what it did; one dropped at the wire is silent
        // everywhere else, and a controller sending nothing looks exactly like a controller
        // whose messages we do not know how to read. Telling those two apart is what found
        // running status, where every message after the first arrived two bytes long and
        // vanished without a word.
        if (msg is null)
        {
            Log.Write(LogArea.Midi, () =>
                "port: '" + device + "' sent " + Bytes(e.Data, e.Start, e.Length)
                + " which is not a note or a knob, so it is dropped here");

            return;
        }

        MessageReceived?.Invoke(this, msg);
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

    /// <summary>
    /// One message off the wire, or nothing when those bytes are not one this host plays.
    /// </summary>
    /// <remarks>
    /// Public because it holds the running status state and is the one piece here with a rule
    /// subtle enough to be worth testing on its own, away from any hardware.
    /// </remarks>
    public MidiMessage? Read(string device, byte[] data, int start, int length)
    {
        if (data == null || length <= 0 || start < 0 || start >= data.Length) return null;

        int at = start;
        int end = Math.Min(start + length, data.Length);

        byte status;

        if (data[at] >= 0x80)
        {
            status = data[at];
            at++;

            // A real time byte can turn up in the middle of anything and changes nothing about
            // what was being sent; a system common one ends the run.
            if (status >= 0xF8) return null;

            if (status >= 0xF0)
            {
                lock (_lock) _running.Remove(device);
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
                if (!_running.TryGetValue(device, out status)) return null;
            }
        }

        // Every kind read here carries two data bytes.
        if (at + 1 >= end) return null;

        int type = status & 0xF0;
        int channel = (status & 0x0F) + 1;

        byte d1 = data[at];
        byte d2 = data[at + 1];

        return type switch
        {
            0x90 => new MidiMessage { Device = device, Type = MidiMessageType.Note, Channel = channel, Value = d1, Data = d2, IsOn = d2 > 0 },
            0x80 => new MidiMessage { Device = device, Type = MidiMessageType.Note, Channel = channel, Value = d1, Data = d2, IsOn = false },
            0xB0 => new MidiMessage { Device = device, Type = MidiMessageType.ControlChange, Channel = channel, Value = d1, Data = d2, IsOn = d2 > 0 },
            _ => null
        };
    }

    private sealed record OpenPort(IMidiInput Input, EventHandler<MidiReceivedEventArgs> Handler);
}
