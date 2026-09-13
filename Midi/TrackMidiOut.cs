using System.Collections.Generic;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi;

/// <inheritdoc/>
/// <remarks>
/// The bytes go out after the lock is let go of, since a port can block and nothing else that
/// wants the lock should wait on a device.
/// </remarks>
public sealed class TrackMidiOut(IMidiService midi, ITrackMidiRoutes? routes = null, IMidiNoteInput? wire = null)
    : ITrackMidiOut
{
    private readonly IMidiService _midi = midi;

    private readonly ITrackMidiRoutes _routes = routes ?? new TrackMidiRoutes();

    private readonly IMidiNoteInput _wire = wire ?? new MidiNoteInput();

    /// <summary>
    /// Where a note played into a track live numbers its voice: this plus its semitone.
    /// </summary>
    /// <remarks>
    /// Past any note column a pattern can have, so a key held on the KeyStep Pro and the pattern
    /// playing the same track never end each other's notes.
    /// </remarks>
    public const int LiveVoices = 1000;

    /// <summary>Guards <see cref="_held"/>.</summary>
    private readonly object _lock = new();

    /// <summary>What each voice is holding: the port, the channel and the note number it went out as.</summary>
    private readonly Dictionary<(int Track, int Voice), (string Port, int Channel, int Number)> _held = new();

    /// <summary>A note on, channel in the low four bits, counted from nought.</summary>
    private const byte NoteOnStatus = 0x90;

    /// <summary>A note off, the same way.</summary>
    private const byte NoteOffStatus = 0x80;

    /// <inheritdoc/>
    public void Prepare(IReadOnlyList<TrackMix>? mix)
    {
        if (mix is null) return;

        var opened = new List<string>();

        for (int track = 0; track < mix.Count; track++)
        {
            if (_routes.OutFor(mix, track) is not { } route) continue;

            string port = route.Port.Trim();
            if (opened.Exists(one => string.Equals(one, port, System.StringComparison.OrdinalIgnoreCase))) continue;

            opened.Add(port);

            if (!_midi.OpenFor(port))
                Log.Write(LogArea.Midi, () => "track midi out: '" + port + "' would not open, so nothing will reach it");
        }
    }

    /// <inheritdoc/>
    public void NoteOn(IReadOnlyList<TrackMix>? mix, int track, int voice, Note note, int velocity)
    {
        if (_routes.OutFor(mix, track) is not { } route) return;
        if (!_wire.TryMidi(note, out int number)) return;

        (string Port, int Channel, int Number) was;
        bool had;
        var now = (route.Port.Trim(), route.Channel, number);

        lock (_lock)
        {
            had = _held.TryGetValue((track, voice), out was);
            _held[(track, voice)] = now;
        }

        if (had) Off(was);

        _midi.Send(now.Item1, new[]
        {
            (byte)(NoteOnStatus | (now.Channel - 1)), (byte)number, (byte)System.Math.Clamp(velocity, 1, 127)
        });
    }

    /// <inheritdoc/>
    public void NoteOff(int track, int voice)
    {
        (string Port, int Channel, int Number) was;

        lock (_lock)
        {
            if (!_held.Remove((track, voice), out was)) return;
        }

        Off(was);
    }

    /// <inheritdoc/>
    public void AllOff()
    {
        List<(string Port, int Channel, int Number)> all;

        lock (_lock)
        {
            all = new List<(string, int, int)>(_held.Values);
            _held.Clear();
        }

        foreach (var one in all) Off(one);
    }

    /// <summary>A note off for what a voice was holding, where it was sent.</summary>
    private void Off((string Port, int Channel, int Number) held) =>
        _midi.Send(held.Port, new[] { (byte)(NoteOffStatus | (held.Channel - 1)), (byte)held.Number, (byte)0 });
}
