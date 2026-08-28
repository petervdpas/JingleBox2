using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Interfaces;

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
    /// <summary>The chain in the order a person edits it. Only ever touched under the lock.</summary>
    private readonly List<Device> _devices = new();

    /// <summary>
    /// Held by every edit and by the one moment a block takes its copy of the list, which is
    /// short enough that the audio thread never waits on a person dragging a device about.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// What the audio thread walks. Rebuilt on the first block after an edit rather than by the
    /// edit itself, so a burst of changes costs one copy instead of one apiece.
    /// </summary>
    private Device[] _snapshot = Array.Empty<Device>();

    /// <summary>True when <see cref="_snapshot"/> is older than <see cref="_devices"/>.</summary>
    private bool _stale = true;

    /// <summary>One box in the chain: what it is, and whether it is switched on.</summary>
    public sealed class Device
    {
        /// <summary>Wraps something that takes audio so the chain can carry it.</summary>
        public Device(IAudioInsert insert) => Insert = insert;

        /// <summary>The thing the audio actually goes through.</summary>
        public IAudioInsert Insert { get; }

        /// <summary>Stepped over while true. The device stays where it is.</summary>
        /// <remarks>
        /// Volatile because it is written by a hand on the UI thread and read on the audio
        /// thread on every block, and a bypass that took a lock to read would put the UI's
        /// contention onto the audio path for a single bit.
        /// </remarks>
        public volatile bool Bypassed;
    }

    /// <summary>How many devices are in the chain, bypassed ones included.</summary>
    public int Count
    {
        get { lock (_lock) return _devices.Count; }
    }

    /// <summary>
    /// The chain in order, as a copy: whoever is reading it is usually about to draw it, and a
    /// list that changed under a redraw would be worse than one that is a moment out of date.
    /// </summary>
    public IReadOnlyList<Device> Devices
    {
        get { lock (_lock) return _devices.ToArray(); }
    }

    /// <summary>Puts something on the end of the chain and hands back its place in it.</summary>
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

    /// <summary>Takes a device out. Nothing happens for one that is not in this chain.</summary>
    public void Remove(Device device)
    {
        lock (_lock)
        {
            _devices.Remove(device);
            _stale = true;
        }
    }

    /// <summary>Moves a device along the chain. Order is the whole point of a chain.</summary>
    /// <returns>
    /// False when the device is not in this chain or the move would run off an end, since being
    /// asked to move the first device up is an ordinary thing for a button to do rather than a
    /// fault.
    /// </returns>
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

    /// <summary>
    /// Empties the chain. Nothing here disposes what was in it: the chain carries devices and
    /// does not own them, and the owner is whoever loaded the plugins.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _devices.Clear();
            _stale = true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A device that throws costs its place in the chain for this block and nothing more: the
    /// rest of the chain still runs, so one misbehaving plugin makes a track sound wrong rather
    /// than making it silent.
    /// </remarks>
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
            }
        }
    }
}
