using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Music;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Operetta, measured rather than listened to.
/// </summary>
/// <remarks>
/// FM is arithmetic that can be checked without ears: an operator heard with nothing bending it is
/// a sine at the note, an operator that only bends is not heard, and bending adds partials that
/// show up as a signal that changes faster from one sample to the next. So what is asked here is
/// whether the wiring really is the wiring each algorithm says, whether a note ends when what is
/// heard has finished, and what happens when it is handed a patch nobody meant to write.
/// </remarks>
public sealed class FmTests
{
    /// <summary>What everything here is rendered at.</summary>
    private const int Rate = 48000;

    /// <summary>A note in the middle of the keyboard, which is where the ratios are measured.</summary>
    private static readonly Note Middle = new(60);

    /// <summary>A patch with every operator silent, on that algorithm.</summary>
    private static FmPatch Quiet(int algorithm = 1)
    {
        var patch = new FmPatch { Algorithm = algorithm, Volume = 1 };

        foreach (var one in patch.Stack)
        {
            one.Level = 0;
            one.Ratio = 1;
            one.Sustain = 1;
            one.AttackMs = 0;
        }

        return patch;
    }

    /// <summary>Renders a voice for that many frames, one side only, in blocks of 512.</summary>
    private static double[] Played(FmVoice voice, int frames)
    {
        var heard = new double[frames];
        var block = new float[512 * 2];

        for (int at = 0; at < frames; at += 512)
        {
            int room = Math.Min(512, frames - at);

            Array.Clear(block);
            voice.Render(block, room);

            for (int i = 0; i < room; i++) heard[at + i] = block[i * 2];
        }

        return heard;
    }

    /// <summary>A voice on that patch, centred and at full gain.</summary>
    private static FmVoice Voice(FmPatch patch) => new(patch, Middle, FmVoice.NoTrack, 1f, 0f, Rate);

    /// <summary>How many times the signal crosses nought after the first tenth of a second.</summary>
    private static int Crossings(double[] audio)
    {
        int count = 0;

        for (int at = (Rate / 10) + 1; at < audio.Length; at++)
            if ((audio[at - 1] < 0) != (audio[at] < 0)) count++;

        return count;
    }

    /// <summary>The loudest sample.</summary>
    private static double Peak(double[] audio) => audio.Max(Math.Abs);

    /// <summary>How much faster than itself the signal moves: the step between samples against the signal.</summary>
    /// <remarks>A sine low down barely moves from one sample to the next; partials high up move a lot.</remarks>
    private static double Brightness(double[] audio)
    {
        double steps = 0, level = 0;

        for (int at = Rate / 10; at < audio.Length; at++)
        {
            steps += (audio[at] - audio[at - 1]) * (audio[at] - audio[at - 1]);
            level += audio[at] * audio[at];
        }

        return steps / Math.Max(level, 1e-12);
    }

    /// <summary>The first operator alone is a sine at the note, at the patch's volume.</summary>
    [Fact]
    public void One_carrier_alone_is_a_sine_at_the_note()
    {
        var patch = Quiet();

        patch.Stack[0].Level = 1;
        patch.Volume = 0.5;

        var heard = Played(Voice(patch), Rate);
        double hz = new NoteFrequency().Hz(Middle);
        double seconds = 0.9;

        Assert.InRange(Crossings(heard), (int)(2 * hz * seconds) - 3, (int)(2 * hz * seconds) + 3);
        Assert.Equal(0.5, Peak(heard), 3);
    }

    /// <summary>The ratio moves the carrier's frequency by that many times.</summary>
    [Fact]
    public void The_ratio_multiplies_the_frequency()
    {
        var once = Quiet();
        var twice = Quiet();

        once.Stack[0].Level = 1;
        twice.Stack[0].Level = 1;
        twice.Stack[0].Ratio = 2;

        double ratio = Crossings(Played(Voice(twice), Rate)) / (double)Crossings(Played(Voice(once), Rate));

        Assert.Equal(2, ratio, 1);
    }

    /// <summary>A modulator bending the carrier makes it brighter.</summary>
    [Fact]
    public void A_modulator_makes_the_carrier_brighter()
    {
        var plain = Quiet();
        var bent = Quiet();

        plain.Stack[0].Level = 1;
        bent.Stack[0].Level = 1;
        bent.Stack[1].Level = 0.7;

        Assert.True(Brightness(Played(Voice(bent), Rate / 2)) > Brightness(Played(Voice(plain), Rate / 2)) * 3);
    }

    /// <summary>An operator that only bends another is not heard.</summary>
    [Fact]
    public void A_modulator_on_its_own_is_not_heard()
    {
        var patch = Quiet();

        patch.Stack[1].Level = 1;
        patch.Stack[3].Level = 1;

        Assert.Equal(0, Peak(Played(Voice(patch), Rate / 4)));
    }

    /// <summary>Four carriers side by side are shared out, so they are no louder than one.</summary>
    [Fact]
    public void Four_carriers_are_no_louder_than_one()
    {
        var one = Quiet(8);
        var four = Quiet(8);

        one.Stack[0].Level = 1;

        foreach (var op in four.Stack) op.Level = 1;

        Assert.Equal(Peak(Played(Voice(one), Rate / 4)) * 4, Peak(Played(Voice(four), Rate / 4)), 3);
    }

    /// <summary>Feedback on the fourth operator turns its sine brighter.</summary>
    [Fact]
    public void Feedback_makes_the_fourth_operator_brighter()
    {
        var plain = Quiet(8);

        plain.Stack[3].Level = 1;

        var fed = Quiet(8);

        fed.Stack[3].Level = 1;
        fed.Feedback = 0.7;

        Assert.True(Brightness(Played(Voice(fed), Rate / 2)) > Brightness(Played(Voice(plain), Rate / 2)) * 2);
    }

    /// <summary>The note ends when what is heard has faded, however long a modulator goes on.</summary>
    [Fact]
    public void The_note_ends_when_its_carriers_have_faded()
    {
        var patch = Quiet();

        patch.Stack[0].Level = 1;
        patch.Stack[0].Sustain = 0;
        patch.Stack[0].DecayMs = 50;
        patch.Stack[1].Level = 0.5;
        patch.Stack[1].DecayMs = 9000;

        var voice = Voice(patch);

        Played(voice, Rate / 5);

        Assert.True(voice.IsFinished);
    }

    /// <summary>A held note lets go when the key comes up, and is gone after its release.</summary>
    [Fact]
    public void A_held_note_lets_go_on_its_release()
    {
        var patch = Quiet();

        patch.Stack[0].Level = 1;
        patch.Stack[0].ReleaseMs = 50;

        var voice = Voice(patch);

        Played(voice, Rate / 5);
        Assert.False(voice.IsFinished);

        voice.NoteOff();
        Played(voice, Rate / 5);

        Assert.True(voice.IsFinished);
    }

    /// <summary>A killed note is silent at once.</summary>
    [Fact]
    public void A_killed_note_is_silent_at_once()
    {
        var patch = Quiet();

        patch.Stack[0].Level = 1;

        var voice = Voice(patch);

        voice.Kill();

        Assert.True(voice.IsFinished);
        Assert.Equal(0, Peak(Played(voice, 512)));
    }

    /// <summary>
    /// Every algorithm is wired the way the voice can work it out: modulators numbered above what
    /// they bend, and something heard.
    /// </summary>
    [Fact]
    public void Every_algorithm_can_be_worked_out_top_down()
    {
        Assert.Equal(FmPatch.Algorithms, FmPatch.ModulatedBy.Count);
        Assert.Equal(FmPatch.Algorithms, FmPatch.Heard.Count);
        Assert.Equal(FmPatch.Algorithms, FmPatch.Drawn.Count);

        for (int algorithm = 0; algorithm < FmPatch.Algorithms; algorithm++)
        {
            Assert.NotEqual(0, FmPatch.Heard[algorithm]);

            for (int op = 0; op < FmPatch.Operators; op++)
            {
                int bentBy = FmPatch.ModulatedBy[algorithm][op];

                Assert.Equal(0, bentBy & ((1 << (op + 1)) - 1));
            }
        }
    }

    /// <summary>Nothing any algorithm can be set to hands back something that is not a number, or more than full.</summary>
    [Fact]
    public void Nothing_it_can_be_set_to_is_louder_than_its_volume_or_not_a_number()
    {
        for (int algorithm = 1; algorithm <= FmPatch.Algorithms; algorithm++)
        {
            var patch = new FmPatch { Algorithm = algorithm, Feedback = 1, Volume = 1 };

            foreach (var op in patch.Stack)
            {
                op.Level = 1;
                op.Ratio = FmOperator.MostRatio;
                op.Sustain = 1;
            }

            var heard = Played(new FmVoice(patch, new Note(108), FmVoice.NoTrack, 1f, 0f, Rate), Rate / 4);

            Assert.All(heard, sample => Assert.True(double.IsFinite(sample)));
            Assert.True(Peak(heard) <= 1.0001, "algorithm " + algorithm + " reached " + Peak(heard));
        }
    }

    /// <summary>A patch holding fewer than four operators, or a hole, plays rather than throwing.</summary>
    [Fact]
    public void A_damaged_patch_plays_and_is_put_back_in_range()
    {
        var patch = new FmPatch { Stack = [new FmOperator { Level = 1 }, null!] };

        var heard = Played(Voice(patch), 2048);

        Assert.All(heard, sample => Assert.True(double.IsFinite(sample)));

        patch.Algorithm = 99;
        patch.Feedback = double.NaN;
        patch.Volume = -3;
        patch.Stack[0].Ratio = 1000;
        patch.Stack[0].Level = double.NaN;

        patch.Clamp();

        Assert.Equal(FmPatch.Operators, patch.Stack.Count);
        Assert.Equal(FmPatch.Algorithms, patch.Algorithm);
        Assert.Equal(0, patch.Feedback);
        Assert.Equal(0, patch.Volume);
        Assert.Equal(FmOperator.MostRatio, patch.Stack[0].Ratio);
        Assert.Equal(0, patch.Stack[0].Level);
    }

    /// <summary>A copy shares nothing, and taking a sound keeps the patch the panel is holding.</summary>
    [Fact]
    public void A_copy_shares_nothing_and_a_preset_lands_in_place()
    {
        var instrument = TrackerInstrument.CreateFm("Keys");
        var copy = instrument.Clone();

        copy.Fm!.Stack[2].Ratio = 7;
        copy.Fm.Algorithm = 5;

        Assert.Equal(1, instrument.Fm!.Stack[2].Ratio);

        var held = instrument.Fm;

        instrument.TakeSoundFrom(copy);

        Assert.Same(held, instrument.Fm);
        Assert.Equal(7, instrument.Fm.Stack[2].Ratio);
        Assert.Equal(5, instrument.Fm.Algorithm);
        Assert.Equal(TrackerInstrumentKind.Fm, instrument.Kind);
    }

    /// <summary>An instrument is written down under the machine's name and read back whole.</summary>
    [Fact]
    public void An_instrument_travels_in_a_file()
    {
        var instrument = TrackerInstrument.CreateFm("Bell");

        instrument.Fm!.Algorithm = 4;
        instrument.Fm.Stack[1].Ratio = 3.5;

        string json = JsonSerializer.Serialize(instrument);

        Assert.Contains("\"Operetta\"", json);

        var back = JsonSerializer.Deserialize<TrackerInstrument>(json)!;

        Assert.Equal(TrackerInstrumentKind.Fm, back.Kind);
        Assert.Equal(4, back.Fm!.Algorithm);
        Assert.Equal(3.5, back.Fm.Stack[1].Ratio);
        Assert.Equal(FmPatch.Operators, back.Fm.Stack.Count);
    }

    /// <summary>The mixer sounds a note on it, and makes room on the track for the next.</summary>
    [Fact]
    public void The_mixer_sounds_it_and_makes_room()
    {
        var mixer = new TrackMixer(Rate);
        var patch = new FmPatch();

        mixer.NoteOn(0, 0, patch, Middle, 1f, 0f);
        mixer.NoteOn(0, 0, patch, new Note(64), 1f, 0f);
        mixer.Preview(patch, Middle, 1f, 0.2, "panel");

        Assert.Equal(3, mixer.VoiceCount);

        mixer.NoteOn(0, 0, (FmPatch)null!, Middle, 1f, 0f);
        mixer.NoteOn(0, 0, patch, Note.Off, 1f, 0f);

        Assert.Equal(3, mixer.VoiceCount);
    }

    /// <summary>Where the shipped Operetta is.</summary>
    private static string Shipped()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", "machines", "Operetta"))) at = at.Parent;

        return at is null ? "" : Path.Combine(at.FullName, "rack", "machines", "Operetta");
    }

    /// <summary>
    /// Every control on the shipped face is a key the panel's values answer, and reads back what it
    /// was set to.
    /// </summary>
    /// <remarks>
    /// A key spelled one way on the face and another in the code is a knob that turns and moves
    /// nothing, silently. Walked off the real file rather than a list written here, so a knob added
    /// to the face is checked without anybody remembering to add it.
    /// </remarks>
    [Fact]
    public void Every_control_on_the_face_moves_the_patch()
    {
        var machine = SoundMachineProject.Open(Shipped());

        Assert.NotNull(machine);
        Assert.Equal(TrackerInstrumentKind.Fm, JingleBox2.SoundDevices.SoundMachines.Records.SoundMachine.EngineNamed(machine!.Engine));

        var instrument = TrackerInstrument.CreateFm("Test");
        var values = new FmValues(instrument.Fm!, instrument);

        Assert.Equal(34, machine.Parameters.Count);

        foreach (var parameter in machine.Parameters)
        {
            double wanted = parameter.Max;

            values.Set(parameter.Key, wanted);

            Assert.True(Math.Abs(values.Get(parameter.Key) - wanted) < 1e-9,
                parameter.Key + " was set to " + wanted + " and reads " + values.Get(parameter.Key));
        }

        Assert.Equal(FmPatch.Algorithms, instrument.Fm!.Algorithm);
        Assert.Equal(0, values.Get("nothing anybody named"));

        values.Set("op2_ratio", double.NaN);
        Assert.Equal(FmOperator.MostRatio, values.Get("op2_ratio"));
    }
}
