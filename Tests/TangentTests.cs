using System;
using System.IO;
using System.Threading;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The two curves the audio path can bend with, and how far apart they are allowed to be.
/// </summary>
/// <remarks>
/// Nothing here measures speed, which is the whole point of the drawn curve and is the one thing
/// a test on a shared machine cannot say anything trustworthy about. What it pins instead is the
/// only reason the drawn one is allowed to exist, which is that it is not a different sound: a
/// bound in decibels, checked over the whole range rather than at a handful of pretty numbers,
/// and the shape facts a saturation curve has to keep whatever it is made of.
///
/// The bound is written down as a number rather than compared with the other curve loosely. A
/// test saying the two are near each other passes just as happily when both have moved.
/// </remarks>
public class TangentTests
{
    private readonly ITangent _exact = new Tangent();
    private readonly ITangent _drawn = new TableTangent();

    /// <summary>How far apart the two curves may be anywhere, as a plain difference.</summary>
    /// <remarks>
    /// 161 dB down, which is below the 144 dB steps a 32-bit float has at full scale, so the
    /// difference is smaller than what the output rounds away on the way past. Worked out rather
    /// than aimed for: the error of a two term Taylor step is the cube of the step over six times
    /// the third derivative, which for this curve is at most two.
    /// </remarks>
    private const double Apart = 1e-8;

    /// <summary>The gap between two neighbouring 32-bit floats at full scale.</summary>
    /// <remarks>
    /// What a sample is rounded to on its way out of this application, so two renderings that
    /// differ by less than this are two renderings the output cannot tell apart.
    /// </remarks>
    private const double FloatStep = 1.1920929e-7;

    /// <summary>The exact one is the system's, and has to be, since that is what off means.</summary>
    [Fact]
    public void The_exact_curve_is_the_system_s_own()
    {
        foreach (double x in new[] { 0.0, 1e-9, 0.25, 1.0, 2.5, 8.1, 12.0, 40.0, -0.5, -7.0 })
            Assert.Equal(Math.Tanh(x), _exact.Of(x));
    }

    /// <summary>
    /// The two agree everywhere the drive can reach, and by how much is the whole argument.
    /// </summary>
    /// <remarks>
    /// Walked at a step far finer than the table's own, so the sweep lands between grid points
    /// rather than on them: on them the drawn curve is the system's answer read back and the test
    /// would be measuring nothing. Out to forty, which is past anything a resonant filter can hand
    /// a drive of ten, so the flat ends are walked as well as the curve.
    /// </remarks>
    [Fact]
    public void The_drawn_curve_is_the_same_curve()
    {
        double worst = 0;

        for (double x = -40; x <= 40; x += 0.0007)
            worst = Math.Max(worst, Math.Abs(_drawn.Of(x) - _exact.Of(x)));

        Assert.True(worst < Apart, $"worst difference {worst:E3}, allowed {Apart:E3}");
    }

    /// <summary>Nought comes back nought, which is what stops a curve leaning.</summary>
    [Fact]
    public void Silence_is_left_alone()
    {
        Assert.Equal(0.0, _drawn.Of(0.0));
        Assert.Equal(0.0, _exact.Of(0.0));
    }

    /// <summary>
    /// The drawn curve is odd exactly, not nearly.
    /// </summary>
    /// <remarks>
    /// Exactly, because a saturation that is a hair louder on one side than the other adds a
    /// direct voltage to whatever goes through it, and a mix carries that to the output where
    /// nothing takes it out again. It is exact because only the positive half is drawn.
    /// </remarks>
    [Fact]
    public void The_drawn_curve_is_odd()
    {
        for (double x = 0; x <= 14; x += 0.0013)
            Assert.Equal(-_drawn.Of(x), _drawn.Of(-x));
    }

    /// <summary>It rises everywhere, which is what makes it a curve rather than a fold.</summary>
    /// <remarks>
    /// A saturation that turned back on itself would send a rising signal down again, which is a
    /// wave folder and a different instrument. Not strictly, since past the reach it is flat.
    /// </remarks>
    [Fact]
    public void The_drawn_curve_only_ever_rises()
    {
        double last = _drawn.Of(-40);

        for (double x = -40; x <= 40; x += 0.0009)
        {
            double now = _drawn.Of(x);

            Assert.True(now >= last, $"fell at {x}: {last} then {now}");

            last = now;
        }
    }

    /// <summary>It reaches one and never passes it, whatever it is handed.</summary>
    [Fact]
    public void The_drawn_curve_stays_inside_full_scale()
    {
        foreach (double x in new[] { 11.9, 12.0, 12.5, 1e6, double.MaxValue, double.PositiveInfinity })
        {
            Assert.InRange(_drawn.Of(x), 0.99, 1.0);
            Assert.InRange(_drawn.Of(-x), -1.0, -0.99);
        }
    }

    /// <summary>
    /// Something that is not a number comes back not a number, rather than as an index.
    /// </summary>
    /// <remarks>
    /// Every comparison against NaN is false, so a guard written as a range test lets it through,
    /// and what it is let through into here is an array index. That is the audio thread, where a
    /// fault is the process gone. The system's own curve answers NaN, so this has to as well or
    /// the switch would change what a poisoned buffer does.
    /// </remarks>
    [Fact]
    public void Nothing_that_is_not_a_number_reaches_the_table()
    {
        Assert.True(double.IsNaN(_drawn.Of(double.NaN)));
        Assert.True(double.IsNaN(_exact.Of(double.NaN)));
    }

    /// <summary>
    /// A driven note rendered both ways is the same note, measured at the output.
    /// </summary>
    /// <remarks>
    /// The claim every test above this one is building towards, made where it can be heard rather
    /// than where it is worked out: a whole note through the engine, on a patch with the drive up
    /// and a resonant filter after it, so the makeup, the fade and the filter all run as they do
    /// in a song. What is compared is the buffer, which is what leaves.
    ///
    /// Against the step a 32-bit float has at full scale rather than against nought, because the
    /// two curves are not the same arithmetic and never will be. Under that step is the whole of
    /// what "not a different sound" can mean for something that has to be written into a float.
    /// </remarks>
    [Fact]
    public void A_driven_note_is_the_same_note_either_way()
    {
        var patch = new Tracker.Synth.SynthPatch
        {
            Wave = Tracker.Synth.Enums.SynthWave.Saw,
            Drive = 8.1,
            FilterCutoffHz = 2000,
            FilterResonance = 0.5,
            AttackMs = 5,
            DecayMs = 50,
            Sustain = 0.8,
            ReleaseMs = 200
        };

        float[] Render(bool fast)
        {
            TangentSwitch.Wants(fast);

            var mixer = new Tracker.Synth.TrackMixer(44100);
            var buffer = new float[4410 * 2];

            mixer.NoteOn(0, 0, patch, new Tracker.Records.Note(48), 1f, 0f,
                         Tracker.Enums.VoiceEnding.Sustain);
            mixer.Render(buffer, 4410);

            return buffer;
        }

        try
        {
            float[] exact = Render(false);
            float[] drawn = Render(true);

            double worst = 0;

            for (int at = 0; at < exact.Length; at++)
                worst = Math.Max(worst, Math.Abs(exact[at] - drawn[at]));

            Assert.True(worst <= FloatStep,
                $"worst difference {worst:E3} at the output, one float step is {FloatStep:E3}");
        }
        finally
        {
            TangentSwitch.Wants(false);
        }
    }

    /// <summary>
    /// The switch says which curve it moved to, in the log, where the render cost is.
    /// </summary>
    /// <remarks>
    /// The line exists so the two curves can be told apart in one file: the switch moves without
    /// stopping the transport, so a song looped over the change has render cost either side of it
    /// and nothing between them but this. It is checked by reading the file back rather than by
    /// trusting the call, because the first time it was written it landed nowhere: it was said at
    /// startup two lines before the log was opened, and a line written into a log that is not open
    /// yet is dropped in silence.
    /// </remarks>
    [Fact]
    public void The_switch_says_which_curve_it_moved_to()
    {
        string folder = Path.Combine(Path.GetTempPath(), "jb-tangent-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        try
        {
            Diagnostics.Log.Open(folder, true, Diagnostics.Enums.LogArea.Audio);

            TangentSwitch.Wants(true);
            TangentSwitch.Wants(false);

            Diagnostics.Log.Close();

            string written = Settled(Path.Combine(folder, Diagnostics.Log.FileName),
                "drive curve: drawn from the table", "drive curve: the system's own");

            Assert.Contains("drive curve: drawn from the table", written);
            Assert.Contains("drive curve: the system's own", written);
        }
        finally
        {
            Diagnostics.Log.Close();
            TangentSwitch.Wants(false);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// Waits for the log's own thread to have written the lines being looked for, then reads them.
    /// </summary>
    /// <remarks>
    /// A line is queued and a thread writes it, so a file read the instant after the call can be
    /// a file that is not there yet or one holding only the first of two lines. **Waiting for the
    /// file to exist is not enough and this test flaked on exactly that**: the file was there and
    /// one of the lines was still in the queue. So what is waited for is the content, which is
    /// the only thing that says the writing is really done.
    ///
    /// Waited for rather than slept past, so this costs a moment rather than a fixed pause, and
    /// given up on after a second so a real fault is a failed assertion naming what is missing
    /// rather than a test that never returns.
    /// </remarks>
    /// <param name="path">The log file to wait for.</param>
    /// <param name="lines">What has to be in it before it counts as written.</param>
    private static string Settled(string path, params string[] lines)
    {
        for (int tries = 0; tries < 100; tries++)
        {
            if (File.Exists(path))
            {
                string held = Read(path);

                if (Array.TrueForAll(lines, held.Contains)) return held;
            }

            Thread.Sleep(10);
        }

        return File.Exists(path) ? Read(path) : "";
    }

    /// <summary>Reads the log while its own thread may still be appending to it.</summary>
    private static string Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(file);

        return reader.ReadToEnd();
    }

    /// <summary>The switch is off until somebody says otherwise, and says which it is on.</summary>
    /// <remarks>
    /// Off is the exact curve and has to be: a switch whose off position is not exactly what
    /// happened before is one nobody can use to decide anything by listening.
    /// </remarks>
    [Fact]
    public void The_switch_starts_on_the_system_s_own()
    {
        try
        {
            TangentSwitch.Wants(false);

            Assert.False(TangentSwitch.Fast);
            Assert.IsType<Tangent>(TangentSwitch.Now);

            TangentSwitch.Wants(true);

            Assert.True(TangentSwitch.Fast);
            Assert.IsType<TableTangent>(TangentSwitch.Now);
        }
        finally
        {
            TangentSwitch.Wants(false);
        }
    }
}
