using System;
using System.Collections.Generic;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
public sealed class PresetLoudness : IPresetLoudness
{
    /// <summary>The rate a preset is measured at.</summary>
    /// <remarks>
    /// Fixed rather than the mixer's, so the same preset answers the same number on every machine
    /// this is run on. A filter's ringing does move a little with the rate, by far less than the
    /// decibel anybody is deciding about here.
    /// </remarks>
    public const int Rate = 48000;

    /// <summary>How much of each note is listened to, in seconds.</summary>
    /// <remarks>
    /// A note is loudest where its attack meets its decay, which is inside the first moment of it
    /// whatever the envelope is set to, so a second reaches the peak of anything and a longer one
    /// would only be rendering a tail nobody is measuring.
    /// </remarks>
    public const double Seconds = 1.0;

    /// <summary>The one seed, so the noise wave answers the same number twice running.</summary>
    private const int Seed = 1;

    /// <inheritdoc/>
    /// <remarks>
    /// Two octaves either side of middle C, on the notes a hand actually falls on. Every fifth
    /// rather than every semitone: the answer moves with the filter and the pitch envelope, both
    /// of which are smooth, so a finer sweep costs render time and finds the same peak.
    /// </remarks>
    public IReadOnlyList<Note> Notes { get; } =
    [
        new Note(24), new Note(31), new Note(36), new Note(43),
        new Note(48), new Note(55), new Note(60), new Note(67), new Note(72),
    ];

    /// <inheritdoc/>
    public double? Peak(TrackerInstrument? sound)
    {
        if (sound is null) return null;

        double worst = 0;

        foreach (var note in Notes)
        {
            var voice = Voice(sound, note);

            if (voice is null) return null;

            worst = Math.Max(worst, Loudest(voice));
        }

        return worst;
    }

    /// <summary>
    /// One note of the preset, ready to render, or nothing when this is not a generated voice.
    /// </summary>
    /// <remarks>
    /// Pan is nought and the note carries no track, since neither is part of what the preset is
    /// worth: a preset panned hard over would read six decibels quieter on one side and be
    /// exactly as loud.
    /// </remarks>
    private static IVoice? Voice(TrackerInstrument sound, Note note) => sound.Kind switch
    {
        TrackerInstrumentKind.Synth =>
            new SynthVoice(sound.Patch, note, SynthVoice.NoTrack, (float)sound.Volume, 0f, Rate, Seed),

        TrackerInstrumentKind.MonoSynth when sound.MonoSynth is not null =>
            new MonoSynthVoice(
                sound.MonoSynth, note, SynthVoice.NoTrack, (float)sound.Volume, 0f, Rate, Seed, null),

        _ => null,
    };

    /// <summary>
    /// The loudest sample the voice reaches over <see cref="Seconds"/>.
    /// </summary>
    /// <remarks>
    /// One block rather than the mixer's, because nothing here is in time with anything: a voice
    /// renders the same samples whether it is asked for them in one go or in two hundred, and one
    /// go is one allocation.
    ///
    /// A voice adds into the buffer rather than filling it, which is what lets the mixer sum
    /// several into one place, so the buffer starts empty and is read as the one voice's own.
    /// </remarks>
    private static double Loudest(IVoice voice)
    {
        int frames = (int)(Rate * Seconds);
        var buffer = new float[frames * 2];

        voice.Render(buffer, frames);

        double worst = 0;

        foreach (float sample in buffer)
        {
            if (!float.IsFinite(sample)) continue;

            worst = Math.Max(worst, Math.Abs(sample));
        }

        return worst;
    }
}
