using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Config;
using JingleBox2.Config.Interfaces;
using JingleBox2.Hints;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The settings file following the settings block, with nobody deciding when.
/// </summary>
/// <remarks>
/// **Told and also looking, which are two halves of one job.** A hint is what makes the file keep
/// up with a hand; comparing what would be written with what was is what makes it right, since a
/// document with lists in it has no arrangement of events that could promise to catch every
/// change. The interesting tests here are the ones where nobody said anything at all.
///
/// Nothing here goes near a disc. What a file is made of is <c>ConfigStore</c>'s and is asked
/// about where the format is; what this is about is when.
/// </remarks>
public sealed class SettingsOnDiscTests
{
    /// <summary>A store that writes to nothing and says what it was asked to write.</summary>
    /// <remarks>
    /// The text is the document's own theme name, which is the shortest thing in
    /// <see cref="AppConfig"/> a test can move: what matters is that it changes when the document
    /// does and not what it is made of.
    /// </remarks>
    private sealed class Paper : IConfigStore
    {
        /// <summary>Everything it was told to write, in order.</summary>
        public List<string> Wrote { get; } = new();

        /// <summary>How many times the text was worked out, hit or miss.</summary>
        public int Asked { get; private set; }

        /// <summary>What the next look throws, for the document being walked as it is changed.</summary>
        public Exception? Throws { get; set; }

        /// <inheritdoc/>
        public string ConfigPath => "";

        /// <inheritdoc/>
        public AppConfig LoadOrCreateDefault() => new();

        /// <inheritdoc/>
        public void Save(AppConfig cfg) => Write(Written(cfg));

        /// <inheritdoc/>
        public string Written(AppConfig cfg)
        {
            Asked++;

            if (Throws is { } bad)
            {
                Throws = null;

                throw bad;
            }

            return cfg.SelectedTheme;
        }

        /// <inheritdoc/>
        public void Write(string written) => Wrote.Add(written);
    }

    /// <summary>Long enough for a clock at a fifth of a second, short enough to fail rather than hang.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    /// <summary>Waits for the writer's own clock to have done something, or gives up.</summary>
    private static bool Within(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            if (done()) return true;

            Thread.Sleep(10);
        }

        return done();
    }

    /// <summary>The first look writes, since what a previous run left cannot be known from here.</summary>
    [Fact]
    public void The_first_look_writes()
    {
        var paper = new Paper();
        var block = new SettingsBlock(new AppConfig { SelectedTheme = "Ember" });

        using var hints = new HintClock();

        var disc = new SettingsOnDisc(paper, block, hints);

        Assert.True(disc.Check());
        Assert.Equal(new[] { "Ember" }, paper.Wrote);
    }

    /// <summary>And a look at settings that have not moved writes nothing.</summary>
    /// <remarks>
    /// The whole of what makes the net affordable: it runs for the life of the application and on
    /// an idle one it is a comparison and no disc at all.
    /// </remarks>
    [Fact]
    public void A_look_at_settings_that_have_not_moved_writes_nothing()
    {
        var paper = new Paper();
        var block = new SettingsBlock(new AppConfig { SelectedTheme = "Ember" });

        using var hints = new HintClock();

        var disc = new SettingsOnDisc(paper, block, hints);

        disc.Check();

        Assert.False(disc.Check());
        Assert.False(disc.Check());
        Assert.Single(paper.Wrote);
    }

    /// <summary>
    /// **A change nobody said a word about is written all the same**, which is the point of it.
    /// </summary>
    /// <remarks>
    /// The fault this replaced: a setting added to the document with nobody remembering the call
    /// that wrote the file, and nothing anywhere saying so. A list added to in place is the same
    /// thing and cannot be said at all.
    /// </remarks>
    [Fact]
    public void A_change_nobody_mentioned_is_still_written()
    {
        var paper = new Paper();
        var settings = new AppConfig { SelectedTheme = "Ember" };
        var block = new SettingsBlock(settings);

        using var hints = new HintClock();

        var disc = new SettingsOnDisc(paper, block, hints);

        disc.Check();

        settings.SelectedTheme = "Citrus";

        Assert.True(disc.Check());
        Assert.Equal(new[] { "Ember", "Citrus" }, paper.Wrote);
    }

    /// <summary>Being told is what makes it quick, and the clock is what does the writing.</summary>
    [Fact]
    public void Saying_it_moved_gets_it_written()
    {
        var paper = new Paper();
        var settings = new AppConfig { SelectedTheme = "Ember" };
        var block = new SettingsBlock(settings);

        using var hints = new HintClock();

        var disc = new SettingsOnDisc(paper, block, hints);

        settings.SelectedTheme = "Neon";
        block.Moved();

        Assert.True(
            Within(() => paper.Wrote.Contains("Neon")),
            "the file was not brought up to date after the settings said they had moved");
    }

    /// <summary>
    /// A look that could not read the document costs one look and comes round again.
    /// </summary>
    /// <remarks>
    /// The document belongs to the drawing thread and this is not it, so a list added to at the
    /// moment it is walked can refuse to be serialised. It is a fraction of a millisecond and the
    /// answer is to come back in a moment; what may not happen is the application going down over
    /// a settings file, from a thread nobody is watching.
    /// </remarks>
    [Fact]
    public void A_document_that_would_not_be_read_costs_one_look()
    {
        var paper = new Paper { Throws = new InvalidOperationException("collection was modified") };
        var block = new SettingsBlock(new AppConfig { SelectedTheme = "Ember" });

        using var hints = new HintClock();

        var disc = new SettingsOnDisc(paper, block, hints);

        Assert.False(disc.Check());
        Assert.Empty(paper.Wrote);

        Assert.True(disc.Check());
        Assert.Equal(new[] { "Ember" }, paper.Wrote);
    }

    /// <summary>
    /// **The way out writes**, since it is the one moment that cannot be waited through.
    /// </summary>
    [Fact]
    public void The_way_out_writes_what_was_left()
    {
        var paper = new Paper();
        var settings = new AppConfig { SelectedTheme = "Ember" };
        var block = new SettingsBlock(settings);

        var hints = new HintClock();

        var disc = new SettingsOnDisc(paper, block, hints);

        disc.Check();

        settings.SelectedTheme = "Orchid";

        hints.Dispose();

        Assert.Equal(new[] { "Ember", "Orchid" }, paper.Wrote);
    }

    /// <summary>And nothing is asked of it once it has been let go of.</summary>
    [Fact]
    public void Letting_it_go_stops_the_clock()
    {
        var paper = new Paper();
        var settings = new AppConfig { SelectedTheme = "Ember" };
        var block = new SettingsBlock(settings);

        var hints = new HintClock();

        var disc = new SettingsOnDisc(paper, block, hints);

        hints.Dispose();

        int asked = paper.Asked;

        settings.SelectedTheme = "Industrial";
        block.Moved();

        Thread.Sleep(500);

        Assert.Equal(asked, paper.Asked);
    }
}
