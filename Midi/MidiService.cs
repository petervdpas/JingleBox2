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
    
    private IMidiInput? _input;

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
                .Select(p => string.IsNullOrWhiteSpace(p.Name) ? p.Id : p.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public void Open(string deviceIdOrName)
    {
        Close();

        if (_access is null) return;
        if (string.IsNullOrWhiteSpace(deviceIdOrName)) return;

        IMidiPortDetails? port;
        try
        {
            port = _access.Inputs.FirstOrDefault(p =>
                string.Equals(p.Id, deviceIdOrName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, deviceIdOrName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return;
        }

        if (port is null) return;

        try
        {
            _input = _access.OpenInputAsync(port.Id).GetAwaiter().GetResult();
            _input.MessageReceived += OnMessageReceived;
        }
        catch
        {
            Close();
        }
    }

    public void Close()
    {
        if (_input is null) return;

        try
        {
            _input.MessageReceived -= OnMessageReceived;
            _input.Dispose();
        }
        finally
        {
            _input = null;
        }
    }

    public void Dispose() => Close();

    private void OnMessageReceived(object? sender, MidiReceivedEventArgs e)
    {
        var msg = Convert(e.Data, e.Start, e.Length);
        if (msg is null) return;
        MessageReceived?.Invoke(this, msg);
    }

    private static MidiMessage? Convert(byte[] data, int start, int length)
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
            0x90 => new MidiMessage { Type = MidiMessageType.Note, Channel = channel, Value = d1, Data = d2, IsOn = d2 > 0 },
            0x80 => new MidiMessage { Type = MidiMessageType.Note, Channel = channel, Value = d1, Data = d2, IsOn = false },
            0xB0 => new MidiMessage { Type = MidiMessageType.ControlChange, Channel = channel, Value = d1, Data = d2, IsOn = d2 > 0 },
            _ => null
        };
    }
}
