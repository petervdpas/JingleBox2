using System;
using System.Collections.Generic;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// Several effects in a row on one piece of audio, the way a desk has a chain of boxes: the
/// first one hears the track, the second hears the first, and so on.
/// </summary>
/// <remarks>
/// The audio thread runs on a snapshot of the list, so adding, removing or reordering while
/// something is playing costs nothing more than the block that was already in flight. A
/// device switched off is stepped over rather than taken out, which is what makes bypass
/// something you can hold down and hear.
/// </remarks>
public sealed class PluginChain : IAudioInsert
{
    private readonly List<Device> _devices = new();
    private readonly object _lock = new();

    private Device[] _snapshot = Array.Empty<Device>();
    private bool _stale = true;

    /// <summary>One box in the chain: what it is, and whether it is switched on.</summary>
    public sealed class Device
    {
        public Device(IAudioInsert insert) => Insert = insert;

        public IAudioInsert Insert { get; }

        /// <summary>Stepped over while true. The device stays where it is.</summary>
        public volatile bool Bypassed;
    }

    public int Count
    {
        get { lock (_lock) return _devices.Count; }
    }

    public IReadOnlyList<Device> Devices
    {
        get { lock (_lock) return _devices.ToArray(); }
    }

    public Device Add(IAudioInsert insert)
    {
        var device = new Device(insert);

        lock (_lock)
        {
            _devices.Add(device);
            _stale = true;
        }

        return device;
    }

    public void Remove(Device device)
    {
        lock (_lock)
        {
            _devices.Remove(device);
            _stale = true;
        }
    }

    /// <summary>Moves a device along the chain. Order is the whole point of a chain.</summary>
    public bool Move(Device device, int offset)
    {
        lock (_lock)
        {
            int from = _devices.IndexOf(device);
            if (from < 0) return false;

            int to = from + offset;
            if (to < 0 || to >= _devices.Count) return false;

            _devices.RemoveAt(from);
            _devices.Insert(to, device);
            _stale = true;

            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _devices.Clear();
            _stale = true;
        }
    }

    public void Process(float[] buffer, int frames)
    {
        Device[] chain;

        lock (_lock)
        {
            if (_devices.Count == 0) return;

            if (_stale)
            {
                _snapshot = _devices.ToArray();
                _stale = false;
            }

            chain = _snapshot;
        }

        foreach (var device in chain)
        {
            if (device.Bypassed) continue;

            try
            {
                device.Insert.Process(buffer, frames);
            }
            catch (Exception)
            {
                // One device misbehaving costs its place in the chain for this block, and
                // the rest of the chain still runs.
            }
        }
    }
}
