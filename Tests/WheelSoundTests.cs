using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Whether a wheel reaches the sound, measured off the rendered block rather than reasoned about.
/// </summary>
/// <remarks>
/// Everything above this is about where a bend is delivered; the only thing that settles whether
/// it does anything is the audio. A pitch is counted off the zero crossings of what was
/// rendered, which is the one reading that cannot be fooled by a number being stored somewhere
/// and never used.
///
/// No hardware and no window: the mixer takes a sample rate and fills a buffer.
/// </remarks>
public class WheelSoundTests
{
    /// <summary>The sample rate everything here is rendered at.</summary>
    private const int Rate = 44100;

    /// <summary>
    /// A block long enough to count a low note's cycles in without the count being noise.
    /// </summary>
    /// <remarks>
    /// A quarter of a second at this rate. Middle C is about 262 cycles a second, so this holds
    /// some sixty five of them and one either way is under two per cent.
    /// </remarks>
    private const int Frames = Rate / 4;

    /// <summary>A patch that certainly makes a noise: on at once and staying on.</summary>
    /// <remarks>
    /// A saw rather than anything richer, since what is being counted is where the wave crosses
    /// nought and a shape with more than one crossing a cycle would be counting something else.
    /// </remarks>
    private static SynthPatch Loud() => new()
    {
        Wave = SynthWave.Saw,
        AttackMs = 0,
        DecayMs = 0,
        Sustain = 1,
        ReleaseMs = 500
    };

    /// <summary>
    /// A wheel held up an octave puts the note an octave up, which is twice the pitch.
    /// </summary>
    /// <remarks>
    /// Twelve semitones rather than the two a wheel usually bends, because the reading is a
    /// count of cycles and an octave is the one interval a count can be wrong about only by
    /// being wrong altogether.
    /// </remarks>
    [Fact]
    public void A_bend_of_an_octave_doubles_the_pitch()
    {
        double plain = Pitch(0);
        double bent = Pitch(12);

        Assert.True(plain > 200, "the plain note should be about middle C, and read " + plain);
        Assert.Equal(2, bent / plain, 1);
    }

    /// <summary>And down an octave halves it, since a wheel leans both ways.</summary>
    [Fact]
    public void And_down_an_octave_halves_it()
    {
        Assert.Equal(0.5, Pitch(-12) / Pitch(0), 1);
    }

    /// <summary>A wheel at rest leaves the note exactly where it was played.</summary>
    /// <remarks>
    /// Bit for bit, which is the claim worth making: a wheel nobody has touched must not cost a
    /// note so much as a rounding, or every song ever written would sound a hair different for
    /// this existing.
    /// </remarks>
    [Fact]
    public void A_wheel_at_rest_changes_not_one_sample()
    {
        var plain = Block(mixer => { });
        var straightened = Block(mixer => mixer.SetBend(0, 0));

        Assert.Equal(plain, straightened);
    }

    /// <summary>
    /// A note struck while the wheel is held arrives already leaning.
    /// </summary>
    /// <remarks>
    /// The half that is easy to leave out, and it comes and goes under the hand: without it the
    /// first note of a phrase played into a bent track is the one note in it at the wrong pitch,
    /// and it straightens itself the moment the wheel is moved again.
    /// </remarks>
    [Fact]
    public void A_note_struck_while_the_wheel_is_held_is_already_bent()
    {
        var mixer = new TrackMixer(Rate);

        mixer.SetBend(0, 12);
        mixer.NoteOn(0, 0, Loud(), new Note(60), 1f, 0f);

        var buffer = new float[Frames * 2];
        mixer.Render(buffer, Frames);

        Assert.Equal(2, Crossings(buffer) / Pitch(0), 1);
    }

    /// <summary>
    /// One track's wheel is one track's.
    /// </summary>
    /// <remarks>
    /// Everything in the mixer is indexed by track number and indexed things go wrong quietly,
    /// which is the whole reason <see cref="MixerIsolationTests"/> exists. A wheel is worse than
    /// most: a hand bending track one while track two plays a part would be heard as the song
    /// going out of tune with nothing on the screen having moved.
    /// </remarks>
    [Fact]
    public void A_wheel_on_one_track_leaves_the_others_where_they_were()
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(1, 0, Loud(), new Note(60), 1f, 0f);
        mixer.SetBend(0, 12);

        var bent = new float[Frames * 2];
        mixer.Render(bent, Frames);

        var alone = new TrackMixer(Rate);
        alone.NoteOn(1, 0, Loud(), new Note(60), 1f, 0f);

        var plain = new float[Frames * 2];
        alone.Render(plain, Frames);

        Assert.Equal(plain, bent);
    }

    /// <summary>
    /// And the loose bus a note played by hand goes onto is not a track either.
    /// </summary>
    /// <remarks>
    /// The rack's keyboard plays an instrument that may be in no song at all, so its notes go to
    /// a bus of their own. Its wheel has to go with them: kept against a track it would bend
    /// whatever that track was playing instead.
    /// </remarks>
    [Fact]
    public void The_racks_own_wheel_is_not_a_tracks()
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(0, 0, Loud(), new Note(60), 1f, 0f);
        mixer.SetBend(SynthVoice.NoTrack, 12);

        var buffer = new float[Frames * 2];
        mixer.Render(buffer, Frames);

        Assert.Equal(Pitch(0), Crossings(buffer), 0);
    }

    /// <summary>
    /// Every machine that ships names a wheel this build can reach, or names none at all.
    /// </summary>
    /// <remarks>
    /// Content rather than code, and it goes wrong the way content goes wrong: a key spelled
    /// differently from the one on the face is read, found to be no parameter of this machine,
    /// and dropped in silence, and what somebody hears is a wheel that does nothing.
    ///
    /// It says out loud when it cannot find the folder, since a test that quietly passes where
    /// its subject is missing reports nothing for the rest of its life.
    /// </remarks>
    [Fact]
    public void Every_shipped_machine_names_a_wheel_it_has()
    {
        string folder = Path.Combine(Root(), "rack", "machines");

        Assert.True(Directory.Exists(folder), "no shipped machines to read at " + folder);

        var manifests = Directory.GetFiles(folder, "machine.json", SearchOption.AllDirectories);

        Assert.NotEmpty(manifests);

        foreach (string path in manifests)
        {
            using var file = JsonDocument.Parse(File.ReadAllText(path));
            var root = file.RootElement;

            if (!root.TryGetProperty("Wheel", out var wheel)) continue;
            if (wheel.GetString() is not { Length: > 0 } key) continue;

            var keys = root.GetProperty("Parameters")
                .EnumerateArray()
                .Select(one => one.TryGetProperty("Key", out var had) ? had.GetString() : null)
                .ToList();

            Assert.True(keys.Contains(key),
                Path.GetFileName(Path.GetDirectoryName(path)) + " points its wheel at '" + key
                + "', which is not one of its parameters");
        }
    }

    /// <summary>
    /// And at a control that means nothing at the bottom, which is where a wheel rests.
    /// </summary>
    /// <remarks>
    /// The one rule a machine can get wrong here. A wheel says how far up it is and nothing
    /// else, so a wheel down is that parameter at its minimum: a vibrato depth is nothing at
    /// nought and the wheel adds it, where a cutoff would slam shut the moment anybody touched
    /// the wheel.
    /// </remarks>
    [Fact]
    public void And_one_that_rests_at_its_own_minimum()
    {
        foreach (string path in Directory.GetFiles(
                     Path.Combine(Root(), "rack", "machines"), "machine.json", SearchOption.AllDirectories))
        {
            using var file = JsonDocument.Parse(File.ReadAllText(path));
            var root = file.RootElement;

            if (!root.TryGetProperty("Wheel", out var wheel)) continue;
            if (wheel.GetString() is not { Length: > 0 } key) continue;

            var parameter = root.GetProperty("Parameters")
                .EnumerateArray()
                .First(one => one.TryGetProperty("Key", out var had) && had.GetString() == key);

            Assert.Equal(parameter.GetProperty("Min").GetDouble(),
                         parameter.GetProperty("Default").GetDouble());
        }
    }

    /// <summary>
    /// A shipped machine that draws a keyboard draws the wheels beside it.
    /// </summary>
    /// <remarks>
    /// **The wheels go with the keys**, which is where they are on every keyboard ever built and
    /// is now the whole rule. It was narrower: only the four that name a control for the
    /// modulation wheel drew them, on the reasoning that drawn without a destination half the
    /// pair is dead and the modulation wheel reads as broken.
    ///
    /// Two things were wrong with that. The pitch wheel reaches every engine here whether or not
    /// anything is named, so half the pair was always live and four faces said nothing about it;
    /// and what the modulation wheel turns is chosen per instrument from the device's own Menu
    /// now, so a machine that names none is a machine with no opinion rather than one with
    /// nothing to offer.
    ///
    /// A rule about what ships rather than about what a machine may do: a device somebody writes
    /// is free to draw keys and no wheels, and its notes still bend.
    /// </remarks>
    [Fact]
    public void A_shipped_machine_that_draws_keys_draws_the_wheels()
    {
        int drawn = 0;

        foreach (string path in Directory.GetFiles(
                     Path.Combine(Root(), "rack", "machines"), "machine.json", SearchOption.AllDirectories))
        {
            var face = JsonDocument.Parse(File.ReadAllText(path)).RootElement
                .GetProperty("Panel").GetProperty("Root");

            if (!Has(face, "Keys")) continue;

            drawn++;

            Assert.True(Has(face, "Wheels"), path + " draws a keyboard and no wheels beside it");
        }

        Assert.Equal(8, drawn);
    }

    /// <summary>Whether a panel has that part anywhere on it.</summary>
    private static bool Has(JsonElement element, string part)
    {
        if (element.TryGetProperty("Element", out var kind) && kind.GetString() == part) return true;

        if (!element.TryGetProperty("Children", out var children)) return false;

        foreach (var child in children.EnumerateArray())
        {
            if (Has(child, part)) return true;
        }

        return false;
    }

    /// <summary>How many times a second the rendered block crosses nought going upwards.</summary>
    /// <remarks>
    /// The left channel alone, since a centred voice puts the same wave in both. Counting
    /// upward crossings rather than all of them makes it one a cycle for a saw whatever its
    /// shape, which is what makes the answer a frequency rather than twice one.
    /// </remarks>
    private static double Crossings(float[] buffer)
    {
        int crossings = 0;

        for (int frame = 1; frame < Frames; frame++)
        {
            if (buffer[(frame - 1) * 2] < 0 && buffer[frame * 2] >= 0) crossings++;
        }

        return crossings / (Frames / (double)Rate);
    }

    /// <summary>What middle C comes out at with the wheel held that far off its pitch.</summary>
    private static double Pitch(float semitones) => Crossings(Block(mixer => mixer.SetBend(0, semitones)));

    /// <summary>One block of middle C on track nought, with something done to the mixer first.</summary>
    private static float[] Block(Action<TrackMixer> first)
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(0, 0, Loud(), new Note(60), 1f, 0f);

        first(mixer);

        var buffer = new float[Frames * 2];
        mixer.Render(buffer, Frames);

        return buffer;
    }

    /// <summary>Where the repository is, from wherever the tests were run.</summary>
    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack"))) at = at.Parent;

        return at?.FullName ?? AppContext.BaseDirectory;
    }
}
