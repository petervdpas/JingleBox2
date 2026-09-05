using System;
using System.IO;
using System.Text.Json;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The two switches that change how a drive behaves, and the promise that neither of them is on
/// unless somebody put it on.
/// </summary>
/// <remarks>
/// Both exist because the drive was measured and found to be doing something its own documentation
/// said it did not. The makeup is worked out from the curve, which maps full scale to full scale,
/// so it holds the height of a wave and says nothing about its area: a saw driven hard is nearly a
/// square, the square is the same height and far fuller, and the knob adds 5.5 dB on the way up.
/// And the filter sits after the drive, so a resonant peak is applied to something that has
/// already been squared off, which is what pushed this machine's presets past full scale.
///
/// Neither is a repair applied behind anybody's back. Every song already written was made against
/// the old behaviour and is entitled to sound exactly as it did, so both default to off and the
/// numbers for off are pinned here: if they move, something changed that nobody asked to change.
/// </remarks>
public class DriveSwitchTests
{
    private const int Rate = 48000;

    /// <summary>The drive and the two switches, on the patch a real song was found crackling on.</summary>
    private static SynthPatch Patch(double drive, bool even, bool filterFirst) => new()
    {
        Wave = SynthWave.Saw,
        AttackMs = 0,
        DecayMs = 2525,
        Sustain = 0.90,
        ReleaseMs = 80,
        TuneSemitones = 7,
        FineCents = -6,
        PitchEnvSemitones = 13,
        PitchEnvMs = 400,
        Drive = drive,
        FilterCutoffHz = 900,
        FilterResonance = 0.30,
        EvenDrive = even,
        FilterFirst = filterFirst,
    };

    /// <summary>One second of one note, as the loudest sample and the root mean square.</summary>
    private static (double Peak, double Rms) Played(SynthPatch patch)
    {
        var voice = new SynthVoice(patch, new Note(56), 0, 1f, 0f, Rate, 1);
        var buffer = new float[Rate * 2];

        voice.Render(buffer, Rate);

        double peak = 0;
        double sum = 0;

        for (int at = 0; at < buffer.Length; at += 2)
        {
            peak = Math.Max(peak, Math.Abs(buffer[at]));
            sum += buffer[at] * (double)buffer[at];
        }

        return (peak, Math.Sqrt(sum / (buffer.Length / 2)));
    }

    /// <summary>How much louder one reading is than another.</summary>
    private static double Decibels(double now, double was) => 20 * Math.Log10(now / was);

    /// <summary>Both switches start off, on a fresh patch and on one read with neither field in it.</summary>
    /// <remarks>
    /// The second half is the one that matters. Every patch on anybody's disc was written before
    /// these existed and says nothing about them, so what a missing field reads as is the whole of
    /// whether their songs change.
    /// </remarks>
    [Fact]
    public void Neither_switch_is_on_unless_somebody_put_it_on()
    {
        Assert.False(new SynthPatch().EvenDrive);
        Assert.False(new SynthPatch().FilterFirst);

        var older = JsonSerializer.Deserialize<SynthPatch>("""{"Drive":8.1,"FilterResonance":0.3}""");

        Assert.NotNull(older);
        Assert.False(older.EvenDrive);
        Assert.False(older.FilterFirst);
    }

    /// <summary>
    /// With both off the voice is exactly what it always was.
    /// </summary>
    /// <remarks>
    /// Pinned to real numbers rather than to a comparison with itself, because the thing being
    /// guarded is a change nobody meant to make. A test that only says the two switches differ
    /// from each other would pass just as happily if both of them had moved.
    /// </remarks>
    [Fact]
    public void Off_is_what_it_has_always_been()
    {
        var quiet = Played(Patch(1, false, false));
        var loud = Played(Patch(8.1, false, false));

        Assert.Equal(0.6979, quiet.Peak, 3);
        Assert.Equal(0.3738, quiet.Rms, 3);
        Assert.Equal(1.0566, loud.Peak, 3);
        Assert.Equal(0.7042, loud.Rms, 3);
    }

    /// <summary>And off, the knob is a level control, which is the fault both switches are about.</summary>
    [Fact]
    public void Off_the_drive_knob_adds_five_and_a_half_decibels()
    {
        double added = Decibels(Played(Patch(8.1, false, false)).Rms, Played(Patch(1, false, false)).Rms);

        Assert.InRange(added, 5.0, 6.0);
    }

    /// <summary>On, it very nearly does not.</summary>
    /// <remarks>
    /// Nearly rather than exactly, and the remainder is honest. The makeup holds what the drive
    /// did to the wave it was handed; the filter after it then passes a different share of a
    /// squared-up spectrum than it passed of a saw, and that is the filter's doing rather than the
    /// knob's. Put the filter first as well and there is nothing after the drive to change its
    /// mind, which is the case below.
    /// </remarks>
    [Fact]
    public void Even_holds_the_loudness_across_the_knob()
    {
        double added = Decibels(Played(Patch(8.1, true, false)).Rms, Played(Patch(1, true, false)).Rms);

        Assert.InRange(added, -1.0, 2.0);
    }

    /// <summary>With both on the knob is level to within a decibel.</summary>
    [Fact]
    public void Both_together_hold_it_to_within_a_decibel()
    {
        double added = Decibels(Played(Patch(8.1, true, true)).Rms, Played(Patch(1, true, true)).Rms);

        Assert.InRange(added, -1.0, 1.0);
    }

    /// <summary>
    /// Either switch on brings the patch back under full scale, which is what was really wrong.
    /// </summary>
    [Fact]
    public void The_switches_bring_the_peak_back_under_full_scale()
    {
        Assert.True(Played(Patch(8.1, false, false)).Peak > 1.0);

        Assert.True(Played(Patch(8.1, true, false)).Peak < 1.0);
        Assert.True(Played(Patch(8.1, true, true)).Peak < 1.0);
    }

    /// <summary>The order switch is a tone control and really does move the sound.</summary>
    /// <remarks>
    /// On its own it makes the patch louder rather than quieter, which is right and is why it is
    /// not sold as the repair: with the filter in front there is nothing after the drive to take
    /// the top off what it made. It is the pair that is the answer.
    /// </remarks>
    [Fact]
    public void The_order_switch_changes_the_sound()
    {
        var after = Played(Patch(8.1, false, false));
        var before = Played(Patch(8.1, false, true));

        Assert.True(Math.Abs(before.Rms - after.Rms) > 0.05);
    }

    /// <summary>With no drive at all the order cannot matter, since one half of it does nothing.</summary>
    [Fact]
    public void With_no_drive_the_order_is_not_a_setting()
    {
        var after = Played(Patch(1, false, false));
        var before = Played(Patch(1, false, true));

        Assert.Equal(after.Peak, before.Peak, 6);
        Assert.Equal(after.Rms, before.Rms, 6);
    }

    /// <summary>A patch carries both switches through a copy and through a preset landing on it.</summary>
    [Fact]
    public void Both_switches_travel_with_the_patch()
    {
        var patch = Patch(8.1, true, true);

        Assert.True(patch.Clone().EvenDrive);
        Assert.True(patch.Clone().FilterFirst);

        var landed = new SynthPatch();

        landed.CopyFrom(patch);

        Assert.True(landed.EvenDrive);
        Assert.True(landed.FilterFirst);
    }

    /// <summary>The curve's own answer to a shape it is handed.</summary>
    /// <remarks>
    /// A square is the case worth pinning at both ends. It is already what a hard drive turns
    /// everything else into, so driving one changes its loudness hardly at all and its makeup is
    /// about one; a saw is what a drive turns *into* a square, so its makeup falls as the knob
    /// goes up and arrives at the saw's own root mean square, which is one over the square root of
    /// three. In between it is neither, since the curve is only nearly vertical at the crossing.
    /// </remarks>
    [Fact]
    public void The_makeup_is_worked_out_from_the_shape()
    {
        var curve = new Saturation();

        var square = new double[256];
        var saw = new double[256];

        for (int at = 0; at < 256; at++)
        {
            square[at] = at < 128 ? 1 : -1;
            saw[at] = 2.0 * (at + 0.5) / 256 - 1;
        }

        Assert.Equal(1.0, curve.Evenly(8.1, square), 2);

        Assert.True(curve.Evenly(8.1, saw) < 1);
        Assert.True(curve.Evenly(24, saw) < curve.Evenly(8.1, saw));
        Assert.Equal(1.0 / Math.Sqrt(3), curve.Evenly(400, saw), 2);
    }

    /// <summary>And the shapes that are not a level.</summary>
    [Fact]
    public void The_makeup_answers_one_where_there_is_nothing_to_hold()
    {
        var curve = new Saturation();

        Assert.Equal(1.0, curve.Evenly(1, new double[] { 1, -1 }));
        Assert.Equal(1.0, curve.Evenly(8.1, Array.Empty<double>()));
        Assert.Equal(1.0, curve.Evenly(8.1, new double[] { 0, 0, 0 }));
        Assert.Equal(1.0, curve.Evenly(double.NaN, new double[] { 1, -1 }));

        Assert.Equal(
            curve.Evenly(8.1, new double[] { 0.5, -0.5 }),
            curve.Evenly(8.1, new double[] { 0.5, double.NaN, -0.5 }),
            6);
    }

    /// <summary>Roaster's own switch holds the loudness of what actually went past.</summary>
    /// <remarks>
    /// An insert cannot be handed the wave it is about to work on, so it measures instead: two
    /// running mean squares either side of the curve. Given a steady tone that is exact, which is
    /// what this says, and given programme it is as close as anything can be that does not know
    /// the future.
    /// </remarks>
    [Theory]
    [InlineData(4.0)]
    [InlineData(24.0)]
    public void Roaster_holds_the_loudness_when_it_is_asked_to(double amount)
    {
        Assert.InRange(Decibels(Roasted(amount, true), Roasted(1, false)), -0.3, 0.3);
        Assert.True(Decibels(Roasted(amount, false), Roasted(1, false)) > 5);
    }

    /// <summary>Two seconds of a tone through Roaster, as the root mean square of the second half.</summary>
    private static double Roasted(double amount, bool even)
    {
        var box = new Drive(Rate);

        box.SetValue(Drive.Amount, amount);
        box.SetValue(Drive.Even, even ? 1 : 0);

        double sum = 0;
        int counted = 0;

        for (int block = 0; block < 40; block++)
        {
            var buffer = new float[512 * 2];

            for (int at = 0; at < 512; at++)
            {
                float value = (float)(0.5 * Math.Sin(2 * Math.PI * 220 * (block * 512 + at) / (double)Rate));

                buffer[at * 2] = value;
                buffer[at * 2 + 1] = value;
            }

            box.Process(buffer, 512);

            if (block < 20) continue;

            for (int at = 0; at < 512; at++)
            {
                sum += buffer[at * 2] * (double)buffer[at * 2];
                counted++;
            }
        }

        return Math.Sqrt(sum / counted);
    }

    /// <summary>Both effects say they have the switches, and their manifests say the same.</summary>
    /// <remarks>
    /// The two lists are written out by hand in two files and have to agree, since a key on the
    /// engine that the face does not name is a control nobody can reach and a key on the face the
    /// engine does not know is a control that silently does nothing.
    /// </remarks>
    [Theory]
    [InlineData("Sweeper", "even")]
    [InlineData("Sweeper", "filter_first")]
    [InlineData("Roaster", "even")]
    public void The_effects_and_their_manifests_agree(string effect, string key)
    {
        var box = effect == "Sweeper" ? new Sweep(Rate) : (JingleBox2.SoundDevices.SoundEffects.Interfaces.ISoundEffectEngine)new Drive(Rate);

        Assert.Contains(key, box.Keys);

        box.SetValue(key, 1);
        Assert.Equal(1, box.ValueOf(key));

        box.SetValue(key, 0);
        Assert.Equal(0, box.ValueOf(key));

        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", "effects"))) at = at.Parent;

        Assert.NotNull(at);

        string manifest = Path.Combine(at.FullName, "rack", "effects", effect, "effect.json");
        using var read = JsonDocument.Parse(File.ReadAllText(manifest));

        Assert.Contains(
            read.RootElement.GetProperty("Parameters").EnumerateArray(),
            one => one.GetProperty("Key").GetString() == key);
    }

    /// <summary>Sweeper's order switch moves the sound and defaults to the order it always had.</summary>
    [Fact]
    public void Sweeper_answers_its_order_switch()
    {
        Assert.Equal(0, new Sweep(Rate).ValueOf(Sweep.FilterFirst));
        Assert.Equal(0, new Sweep(Rate).ValueOf(Sweep.Even));

        Assert.True(Math.Abs(Swept(false) - Swept(true)) > 0.05);
    }

    /// <summary>Two seconds of a tone through Sweeper, as the root mean square of the second half.</summary>
    private static double Swept(bool filterFirst)
    {
        var box = new Sweep(Rate);

        box.SetValue(Sweep.Cutoff, 300);
        box.SetValue(Sweep.Drive, 12);
        box.SetValue(Sweep.FilterFirst, filterFirst ? 1 : 0);

        double sum = 0;
        int counted = 0;

        for (int block = 0; block < 40; block++)
        {
            var buffer = new float[512 * 2];

            for (int at = 0; at < 512; at++)
            {
                float value = (float)(0.5 * Math.Sin(2 * Math.PI * 220 * (block * 512 + at) / (double)Rate));

                buffer[at * 2] = value;
                buffer[at * 2 + 1] = value;
            }

            box.Process(buffer, 512);

            if (block < 20) continue;

            for (int at = 0; at < 512; at++)
            {
                sum += buffer[at * 2] * (double)buffer[at * 2];
                counted++;
            }
        }

        return Math.Sqrt(sum / counted);
    }

    /// <summary>The measured makeup on its own: silence, poison, and the ends it is held to.</summary>
    [Fact]
    public void The_measured_makeup_refuses_what_it_cannot_read()
    {
        var makeup = new LoudnessMakeup(Rate);

        Assert.Equal(1, makeup.Makeup);

        for (int at = 0; at < Rate; at++) makeup.Saw(0, 0);
        Assert.Equal(1, makeup.Makeup);

        var poisoned = new LoudnessMakeup(Rate);

        for (int at = 0; at < Rate; at++) poisoned.Saw(double.NaN, double.PositiveInfinity);
        Assert.Equal(1, poisoned.Makeup);

        var halved = new LoudnessMakeup(Rate);

        for (int at = 0; at < Rate; at++) halved.Saw(0.5, 0.25);
        Assert.Equal(2.0, halved.Makeup, 2);

        var crushed = new LoudnessMakeup(Rate);

        for (int at = 0; at < Rate; at++) crushed.Saw(1.0, 0.0001);
        Assert.Equal(LoudnessMakeup.Loudest, crushed.Makeup);
    }
}
