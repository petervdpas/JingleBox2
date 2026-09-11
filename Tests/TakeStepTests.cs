using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Audio;
using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Turning one written-down step into the edit it asks for.
/// </summary>
/// <remarks>
/// A step is done twice in the life of an edit, once when somebody asks for it and again every
/// time the history is wound back past it, and both go through here. What would go wrong quietly
/// is a kind reaching the wrong call: a fade out that faded in is an edit nobody would notice
/// until the take was used, and by then the history has been replayed over it a dozen times.
/// </remarks>
public sealed class TakeStepTests
{
    /// <summary>A waveform service that does nothing and writes down what it was asked.</summary>
    private sealed class Told : IWaveformService
    {
        /// <summary>What was asked, in order, as a word apiece.</summary>
        public List<string> Calls { get; } = new();

        /// <summary>What the next normalize answers, in decibels.</summary>
        public double Moves { get; set; } = 3;

        /// <inheritdoc/>
        public WaveformData AnalyzeFile(string filePath) => new()
        {
            PeakData = Array.Empty<float>(),
            SampleRate = 44100,
            Channels = 1,
            TotalSamples = 0
        };

        /// <inheritdoc/>
        public TimeSpan GetDuration(string filePath) => TimeSpan.Zero;

        /// <inheritdoc/>
        public long GetFrameCount(string filePath) => 0;

        /// <inheritdoc/>
        public void TrimFile(string filePath, long startFrame, long endFrame) =>
            Calls.Add($"trim {startFrame} {endFrame}");

        /// <inheritdoc/>
        public void SilenceFile(string filePath, long startFrame, long endFrame) =>
            Calls.Add($"silence {startFrame} {endFrame}");

        /// <inheritdoc/>
        public void ReverseFile(string filePath, long startFrame, long endFrame) =>
            Calls.Add($"reverse {startFrame} {endFrame}");

        /// <inheritdoc/>
        public void FadeFile(string filePath, long startFrame, long endFrame, bool rising) =>
            Calls.Add($"fade {(rising ? "in" : "out")} {startFrame} {endFrame}");

        /// <inheritdoc/>
        public double NormalizeFile(string filePath, double targetDecibels)
        {
            Calls.Add($"normalize {targetDecibels}");

            return Moves;
        }
    }

    /// <summary>What the calls were made against.</summary>
    private readonly Told _told = new();

    /// <summary>The runner under test.</summary>
    private readonly ITakeSteps _steps;

    /// <inheritdoc cref="TakeStepTests"/>
    public TakeStepTests() => _steps = new TakeSteps(_told);

    /// <summary>Each kind reaches its own call, with the region it was given.</summary>
    /// <param name="kind">Which edit.</param>
    /// <param name="expected">What the service should have been asked.</param>
    [Theory]
    [InlineData(TakeEditKind.Trim, "trim 10 40")]
    [InlineData(TakeEditKind.Silence, "silence 10 40")]
    [InlineData(TakeEditKind.Reverse, "reverse 10 40")]
    [InlineData(TakeEditKind.FadeIn, "fade in 10 40")]
    [InlineData(TakeEditKind.FadeOut, "fade out 10 40")]
    public void A_step_reaches_its_own_call(TakeEditKind kind, string expected)
    {
        Assert.True(_steps.Run(new TakeStep(kind, 10, 40, -1), "take.wav"));

        Assert.Equal(new[] { expected }, _told.Calls);
    }

    /// <summary>A normalize carries its own peak rather than reading one from anywhere else.</summary>
    /// <remarks>
    /// Which is what stops a peak changed in the box afterwards rewriting what an old step did
    /// when the history is replayed over it.
    /// </remarks>
    [Fact]
    public void A_normalize_carries_the_peak_it_was_asked_for()
    {
        _steps.Run(new TakeStep(TakeEditKind.Normalize, 0, 0, -6), "take.wav");

        Assert.Equal(new[] { "normalize -6" }, _told.Calls);
    }

    /// <summary>A normalize of a take already on its peak is not a step at all.</summary>
    [Fact]
    public void A_normalize_that_moves_nothing_answers_no()
    {
        _told.Moves = 0;

        Assert.False(_steps.Run(new TakeStep(TakeEditKind.Normalize, 0, 0, -1), "take.wav"));
    }

    /// <summary>Every kind there is has a call, which is what stops one being added and forgotten.</summary>
    [Fact]
    public void Every_kind_does_something()
    {
        foreach (TakeEditKind kind in Enum.GetValues<TakeEditKind>())
        {
            _told.Calls.Clear();

            _steps.Run(new TakeStep(kind, 0, 10, -1), "take.wav");

            Assert.True(_told.Calls.Count == 1, kind + " asked for nothing");
        }
    }

    /// <summary>And every kind has a word on it, since a history nobody can read is furniture.</summary>
    [Fact]
    public void Every_kind_has_a_word_of_its_own()
    {
        ITakeStepWords words = new TakeStepWords();

        var said = Enum.GetValues<TakeEditKind>().Select(words.For).ToList();

        Assert.DoesNotContain(said, word => string.IsNullOrWhiteSpace(word));
        Assert.Equal(said.Count, said.Distinct().Count());
    }
}
