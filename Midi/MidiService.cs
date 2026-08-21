using System;
using System.Collections.Generic;
using System.Linq;
using Commons.Music.Midi;

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
                string.Equals(p.Id, deviceIdOrName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, deviceIdOrName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }

        if (port is null) return false;

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
        catch
        {
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
        return true;
    }

    public void Close(string deviceIdOrName)
    {
        if (string.IsNullOrWhiteSpace(deviceIdOrName)) return;

        OpenPort? port;
        lock (_lock)
        {
            if (!_ports.Remove(deviceIdOrName, out port)) return;
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

    private static string DisplayName(IMidiPortDetails port) =>
        string.IsNullOrWhiteSpace(port.Name) ? port.Id : port.Name;

    private void OnMessageReceived(string device, MidiReceivedEventArgs e)
    {
        var msg = Convert(device, e.Data, e.Start, e.Length);
        if (msg is null) return;
        MessageReceived?.Invoke(this, msg);
    }

    private static MidiMessage? Convert(string device, byte[] data, int start, int length)
    {
        if (data == null || length <= 0 || start < 0 || start >= data.Length) return null;
        if (start + 2 >= data.Length) return null;

        byte status = data[start];
        int type = status & 0xF0;
        int channel = (status & 0x0F) + 1;

        byte d1 = data[start + 1];
        byte d2 = data[start + 2];

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
