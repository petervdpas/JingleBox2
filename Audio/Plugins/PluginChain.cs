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
public sealed class PluginChain : IAudioInsert, IOverlappable
{
    /// <summary>The chain in the order a person edits it. Only ever touched under the lock.</summary>
    private readonly List<Slot> _devices = new();

    /// <summary>
    /// Held by every edit and by the one moment a block takes its copy of the list, which is
    /// short enough that the audio thread never waits on a person dragging a device about.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// What the audio thread walks. Rebuilt on the first block after an edit rather than by the
    /// edit itself, so a burst of changes costs one copy instead of one apiece.
    /// </summary>
    private Slot[] _snapshot = Array.Empty<Slot>();

    /// <summary>True when <see cref="_snapshot"/> is older than <see cref="_devices"/>.</summary>
    private bool _stale = true;

    /// <summary>One box in the chain: what it is, and whether it is switched on.</summary>
    public sealed class Slot
    {
        /// <summary>Wraps something that takes audio so the chain can carry it.</summary>
        public Slot(IAudioInsert insert) => Insert = insert;

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
    public IReadOnlyList<Slot> Slots
    {
        get { lock (_lock) return _devices.ToArray(); }
    }

    /// <summary>Puts something on the end of the chain and hands back its place in it.</summary>
    public Slot Add(IAudioInsert insert)
    {
        var device = new Slot(insert);

        lock (_lock)
        {
            _devices.Add(device);
            _stale = true;
        }

        return device;
    }

    /// <summary>Takes a device out. Nothing happens for one that is not in this chain.</summary>
    public void Remove(Slot device)
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
    public bool Move(Slot device, int offset)
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
        Slot[] chain;

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

    /// <summary>The chain this run is walking, or nothing when no run is in flight.</summary>
    /// <remarks>
    /// The snapshot taken when the run began rather than the live list, so a device dropped onto
    /// the chain halfway through a block does not appear in the middle of that block's audio.
    /// </remarks>
    private Slot[]? _run;

    /// <summary>How far along that snapshot the run has got.</summary>
    private int _at;

    /// <summary>The device whose work is in flight, or nothing between rounds.</summary>
    private IOverlappable? _flying;

    /// <inheritdoc/>
    /// <remarks>
    /// A run left half finished is finished here before a new one starts. That cannot happen
    /// while the mixer drives this to the end, which it does, and it costs one comparison to be
    /// certain: a device left holding an answer nobody collected would refuse every block after
    /// it, for the rest of the session, and the symptom would be one plugin going silent for no
    /// reason anybody could see.
    /// </remarks>
    public bool Begin(float[] buffer, int frames)
    {
        Settle(buffer, frames);

        Slot[] chain;

        lock (_lock)
        {
            if (_devices.Count == 0) return false;

            if (_stale)
            {
                _snapshot = _devices.ToArray();
                _stale = false;
            }

            chain = _snapshot;
        }

        _run = chain;
        _at = 0;

        return Push(buffer, frames);
    }

    /// <inheritdoc/>
    public bool Advance(float[] buffer, int frames)
    {
        Settle(buffer, frames);

        return Push(buffer, frames);
    }

    /// <summary>Collects whatever is in flight, whatever happened to it.</summary>
    /// <param name="buffer">The audio the run is on.</param>
    /// <param name="frames">How many frames are in it.</param>
    private void Settle(float[] buffer, int frames)
    {
        var flying = _flying;

        _flying = null;

        if (flying == null) return;

        try
        {
            flying.Advance(buffer, frames);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Walks on until something is in flight or the chain is finished.
    /// </summary>
    /// <remarks>
    /// A device that cannot be left in flight is simply done here, which is every effect of ours,
    /// every bypassed slot, and a plugin handed a block too long to cross in one go. So a chain
    /// of our own effects behaves exactly as it always did and never reports anything in flight.
    /// </remarks>
    /// <param name="buffer">The audio the run is on.</param>
    /// <param name="frames">How many frames are in it.</param>
    /// <returns>Whether something is now in flight.</returns>
    private bool Push(float[] buffer, int frames)
    {
        var chain = _run;

        if (chain == null) return false;

        while (_at < chain.Length)
        {
            var device = chain[_at++];

            if (device.Bypassed) continue;

            if (device.Insert is IOverlappable overlappable)
            {
                bool flying;

                try
                {
                    flying = overlappable.Begin(buffer, frames);
                }
                catch (Exception)
                {
                    flying = false;
                }

                if (flying)
                {
                    _flying = overlappable;
                    return true;
                }
            }

            try
            {
                device.Insert.Process(buffer, frames);
            }
            catch (Exception)
            {
            }
        }

        _run = null;

        return false;
    }
}
