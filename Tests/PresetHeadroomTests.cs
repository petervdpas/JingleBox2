using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Rack.SoundDevices;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How loud the machines that ship are, measured by playing them rather than by reading a number
/// off a file.
/// </summary>
/// <remarks>
/// The fault this is here for is one nobody notices until a song has three tracks in it. Every
/// OddSkilla preset shipped at full scale on one note, so the second note was already past the
/// end and the master's saturation was shaping four fifths of the waveform, which is heard as the
/// whole mix crackling whenever anything played at once. Nothing in the files said so: the level
/// knob read nought, and the drive, the resonance and the envelope had added the rest.
///
/// So a level cannot be checked by reading it. What is checked is the peak the engine really
/// produces, which is <see cref="PresetLoudness"/>, against the room a device has to leave, which
/// is <see cref="Headroom"/>.
/// </remarks>
public class PresetHeadroomTests
{
    /// <summary>The rule, which knows nothing about audio.</summary>
    private readonly Headroom _room = new();

    /// <summary>The measurement, which renders.</summary>
    private readonly PresetLoudness _loudness = new();

    /// <summary>How a shipped preset file is read, the same way the application reads one.</summary>
    private readonly SoundMachinePresetFile _files = new();

    /// <summary>Where the shipped machines are, walking up out of the test's own output.</summary>
    private static string Shipped
    {
        get
        {
            var at = new DirectoryInfo(AppContext.BaseDirectory);

            while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", "machines")))
                at = at.Parent;

            return at is null ? "" : Path.Combine(at.FullName, "rack", "machines");
        }
    }

    /// <summary>Every shipped preset that this build can render, with what it was read from.</summary>
    private IEnumerable<(string Name, TrackerInstrument Sound)> Generated()
    {
        foreach (string folder in Directory.EnumerateDirectories(Shipped).OrderBy(one => one, StringComparer.Ordinal))
        {
            if (SoundMachineProject.Open(folder) is not { } machine) continue;

            string presets = Path.Combine(folder, "presets");

            if (!Directory.Exists(presets)) continue;

            foreach (string file in Directory.EnumerateFiles(presets, "*.json").OrderBy(one => one, StringComparer.Ordinal))
            {
                if (_files.Read(file, machine) is not { } sound) continue;
                if (_loudness.Peak(sound) is null) continue;

                yield return (Path.GetFileName(folder) + " " + Path.GetFileNameWithoutExtension(file), sound);
            }
        }
    }

    /// <summary>
    /// The shipped folder is where the test expects it, and there are presets in it that render.
    /// </summary>
    /// <remarks>
    /// Said out loud rather than skipped past. Every other test here walks what it finds, so a
    /// checkout where the folder moved would leave all of them passing over an empty list and
    /// reporting nothing for the rest of their lives. That has already happened once in this
    /// repository, to the release workflow's own count of the rack.
    /// </remarks>
    [Fact]
    public void The_shipped_machines_are_where_they_are_looked_for()
    {
        Assert.True(Directory.Exists(Shipped), "rack/machines was not found above " + AppContext.BaseDirectory);
        Assert.NotEmpty(Generated());
    }

    /// <summary>
    /// No shipped preset reaches full scale on one note.
    /// </summary>
    /// <remarks>
    /// The one that would fail is named, because a test saying twenty eight presets are too loud
    /// sends somebody to read twenty eight files.
    /// </remarks>
    [Fact]
    public void Every_shipped_preset_leaves_room()
    {
        foreach (var (name, sound) in Generated())
        {
            double peak = _loudness.Peak(sound) ?? 0;

            Assert.False(
                _room.Cramped(peak),
                $"{name} peaks at {peak:F3}, which leaves {_room.Room(peak):F1} dB " +
                $"where {_room.Least:F0} dB is the least a device should leave");
        }
    }

    /// <summary>
    /// And none of them is so quiet that nobody would hear it either.
    /// </summary>
    /// <remarks>
    /// The other end of the same rule, and it is worth pinning because the fix for the first one
    /// is to turn everything down: a preset dropped past what the ceiling asked for is one
    /// somebody has to reach for the fader to hear at all, and a bank where that has happened to
    /// half of it sounds broken rather than quiet. Twelve decibels of slack under the ceiling,
    /// which is a whole doubling and then some.
    /// </remarks>
    [Fact]
    public void No_shipped_preset_is_inaudible()
    {
        foreach (var (name, sound) in Generated())
        {
            double peak = _loudness.Peak(sound) ?? 0;

            Assert.True(
                _room.Room(peak) < _room.Least + 12,
                $"{name} peaks at {peak:F3}, which is {_room.Room(peak):F1} dB down and too quiet to pick");
        }
    }

    /// <summary>A preset that plays somebody's recording has no loudness of the machine's own.</summary>
    /// <remarks>
    /// The four kinds that are not generated are the whole reason <see cref="PresetLoudness.Peak"/>
    /// answers nothing rather than nought: a sampler, a kit and a recording are as loud as the
    /// take they play, and a plugin is another program. Reporting a number for any of those would
    /// send whoever read it to change a knob that is not the cause.
    /// </remarks>
    [Fact]
    public void A_preset_that_is_not_generated_has_no_answer()
    {
        Assert.Null(_loudness.Peak(null));
        Assert.Null(_loudness.Peak(TrackerInstrument.CreateSampler("s")));
        Assert.Null(_loudness.Peak(TrackerInstrument.CreateKit("k")));
        Assert.Null(_loudness.Peak(TrackerInstrument.CreateSample("r", "nowhere.wav", Tracker.Records.Note.C4)));
    }

    /// <summary>The rule reads a peak the way a meter is read, and never answers infinity.</summary>
    /// <remarks>
    /// Silence is the case worth pinning. Every other reading here is a logarithm of a number
    /// between nought and one, and the logarithm of nought is the one answer no screen can draw.
    /// </remarks>
    [Theory]
    [InlineData(1.0, 0.0)]
    [InlineData(0.5, 6.0)]
    [InlineData(0.25, 12.0)]
    [InlineData(2.0, -6.0)]
    public void Room_is_decibels_under_full_scale(double peak, double expected)
    {
        Assert.Equal(expected, _room.Room(peak), 1);
    }

    /// <summary>Silence, a peak the wrong way round, and a reading that is not a number.</summary>
    [Fact]
    public void The_readings_that_are_not_a_level()
    {
        Assert.Equal(Headroom.Quietest, _room.Room(0));
        Assert.Equal(Headroom.Quietest, _room.Room(-0.0));
        Assert.Equal(_room.Room(0.5), _room.Room(-0.5), 6);
        Assert.Equal(0, _room.Room(double.NaN));
        Assert.True(_room.Cramped(double.NaN));
        Assert.False(_room.Cramped(0));
    }

    /// <summary>The ceiling is where the rule says it is, on both sides of it.</summary>
    [Fact]
    public void Cramped_is_the_ceiling_and_not_a_hair_off_it()
    {
        Assert.False(_room.Cramped(0.25));
        Assert.True(_room.Cramped(0.26));
        Assert.False(_room.Cramped(0.24));
    }

    /// <summary>Measuring the same preset twice answers the same number.</summary>
    /// <remarks>
    /// The noise wave is the reason. A voice seeds its own noise so two noise notes do not agree,
    /// which is right when they are being played and would make this reading wander if it were
    /// left to the clock, so the measurement pins the seed.
    /// </remarks>
    [Fact]
    public void The_measurement_is_repeatable()
    {
        var sound = TrackerInstrument.CreateSynth("noise");
        sound.Patch.Wave = Tracker.Synth.Enums.SynthWave.Noise;

        Assert.Equal(_loudness.Peak(sound), _loudness.Peak(sound));
    }
}
