using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Hints;
using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Midi.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The templates file following the templates block, with nobody deciding when.
/// </summary>
/// <remarks>
/// **Told and also looking.** A hint is what makes the file keep up with a hand; comparing what
/// would be written with what was is what makes it right, and here the second half earns its keep
/// on its own: a link learned says it moved, and the router deciding what kind of control is
/// sending changes a template's pickup from the MIDI thread with nobody having asked for
/// anything. So the interesting tests are the ones where nobody said a word.
///
/// Nothing here goes near a disc. What a file is made of is the store's and is asked about where
/// the format is; what this is about is when.
/// </remarks>
public sealed class ControlTemplatesOnDiscTests
{
    /// <summary>A store that writes to nothing and says what it was asked to write.</summary>
    private sealed class Paper : IControlTemplates
    {
        /// <summary>Everything it was told to write, in order.</summary>
        public List<string> Wrote { get; } = new();

        /// <summary>How many times the text was worked out, hit or miss.</summary>
        public int Asked { get; private set; }

        /// <summary>What the next look throws, for a block being walked as it is filled.</summary>
        public Exception? Throws { get; set; }

        /// <inheritdoc/>
        public string Written(IEnumerable<ControlTemplate>? templates)
        {
            Asked++;

            if (Throws is { } bad)
            {
                Throws = null;

                throw bad;
            }

            var said = new List<string>();

            foreach (var one in templates ?? new List<ControlTemplate>())
                said.Add(one.Controller + "/" + one.Target.Id + "/" + one.Controls.Count);

            return string.Join(",", said);
        }

        /// <inheritdoc/>
        public void Keep(string written) => Wrote.Add(written);

        /// <inheritdoc/>
        public IReadOnlyList<ControlTemplate> Read() => Array.Empty<ControlTemplate>();

        /// <inheritdoc/>
        public bool Covers(ControlTemplate? template, ControlMapping? one, Func<string, string>? called = null) => false;

        /// <inheritdoc/>
        public string Kept() => "";

        /// <inheritdoc/>
        public string Folder() => "";

        /// <inheritdoc/>
        public string FileName(ControlTemplate template) => "template";

        /// <inheritdoc/>
        public ControlTemplate? Describe(
            string controller, IEnumerable<ControlMapping> links, Func<int, int, string>? named = null) => null;

        /// <inheritdoc/>
        public IReadOnlyList<ControlTemplate> Cut(
            IEnumerable<ControlMapping>? links,
            Func<string, string>? called = null,
            Func<string, int, int, string>? named = null) => Array.Empty<ControlTemplate>();

        /// <inheritdoc/>
        public ControlTemplateReading Take(
            ControlTemplate? template,
            IEnumerable<string>? ports = null,
            Func<string, string>? called = null) =>
            new(Array.Empty<ControlMapping>(), 0, "", false);

        /// <inheritdoc/>
        public void Write(string path, ControlTemplate template)
        {
        }

        /// <inheritdoc/>
        public ControlTemplate? Open(string path) => null;
    }

    /// <summary>One controller's layout for one machine.</summary>
    private static ControlTemplate Template(string controller, string machine, int controls = 1)
    {
        var made = new ControlTemplate
        {
            Controller = controller,
            Target = new ControlTemplateTarget { Kind = "sounddevice", Id = machine, Name = machine },
        };

        for (int one = 0; one < controls; one++)
            made.Controls.Add(new ControlTemplateControl { Cc = 16 + one, Channel = 1, Parameter = "knob" });

        return made;
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
        var block = new ControlTemplateBlock(new List<ControlTemplate> { Template("nanoKONTROL2", "machine.a") });

        using var hints = new HintClock();

        var disc = new ControlTemplatesOnDisc(paper, block, hints);

        Assert.True(disc.Check());
        Assert.Equal(new[] { "nanoKONTROL2/machine.a/1" }, paper.Wrote);
    }

    /// <summary>And a look at templates that have not moved writes nothing.</summary>
    /// <remarks>
    /// The whole of what makes the net affordable: it runs for the life of the application and on
    /// an idle one it is a comparison and no disc at all.
    /// </remarks>
    [Fact]
    public void A_look_at_templates_that_have_not_moved_writes_nothing()
    {
        var paper = new Paper();
        var block = new ControlTemplateBlock(new List<ControlTemplate> { Template("nanoKONTROL2", "machine.a") });

        using var hints = new HintClock();

        var disc = new ControlTemplatesOnDisc(paper, block, hints);

        disc.Check();

        Assert.False(disc.Check());
        Assert.False(disc.Check());
        Assert.Single(paper.Wrote);
    }

    /// <summary>
    /// **A template that moved with nobody saying so is written all the same**, which is what the
    /// net is for.
    /// </summary>
    /// <remarks>
    /// The case it exists for is real and is not a gesture: the router works out what kind of
    /// control is sending and writes the pickup onto the mapping, from the MIDI thread, with
    /// nobody having pointed at anything.
    /// </remarks>
    [Fact]
    public void A_template_that_moved_unasked_is_still_written()
    {
        var paper = new Paper();
        var block = new ControlTemplateBlock(new List<ControlTemplate> { Template("nanoKONTROL2", "machine.a") });

        using var hints = new HintClock();

        var disc = new ControlTemplatesOnDisc(paper, block, hints);

        disc.Check();

        block.Templates[0].Controls.Add(new ControlTemplateControl { Cc = 20, Channel = 1 });

        Assert.True(disc.Check());
        Assert.Equal(new[] { "nanoKONTROL2/machine.a/1", "nanoKONTROL2/machine.a/2" }, paper.Wrote);
    }

    /// <summary>Being told is what makes it quick, and the clock is what does the writing.</summary>
    [Fact]
    public void Saying_it_moved_gets_it_written()
    {
        var paper = new Paper();
        var block = new ControlTemplateBlock();

        using var hints = new HintClock();

        var disc = new ControlTemplatesOnDisc(paper, block, hints);

        block.Templates.Add(Template("MiniLab3", "machine.b"));
        block.Moved();

        Assert.True(
            Within(() => paper.Wrote.Contains("MiniLab3/machine.b/1")),
            "the file was not brought up to date after the templates said they had moved");
    }

    /// <summary>A look that could not read the block costs one look and comes round again.</summary>
    [Fact]
    public void A_block_that_would_not_be_read_costs_one_look()
    {
        var paper = new Paper { Throws = new InvalidOperationException("collection was modified") };
        var block = new ControlTemplateBlock(new List<ControlTemplate> { Template("nanoKONTROL2", "machine.a") });

        using var hints = new HintClock();

        var disc = new ControlTemplatesOnDisc(paper, block, hints);

        Assert.False(disc.Check());
        Assert.Empty(paper.Wrote);

        Assert.True(disc.Check());
        Assert.Single(paper.Wrote);
    }

    /// <summary>**The way out writes**, since it is the one moment that cannot be waited through.</summary>
    [Fact]
    public void The_way_out_writes_what_was_left()
    {
        var paper = new Paper();
        var block = new ControlTemplateBlock();

        var hints = new HintClock();

        var disc = new ControlTemplatesOnDisc(paper, block, hints);

        disc.Check();

        block.Templates.Add(Template("nanoKONTROL2", "machine.c"));

        hints.Dispose();

        Assert.Contains("nanoKONTROL2/machine.c/1", paper.Wrote);
    }

    /// <summary>And nothing is asked of it once it has been let go of.</summary>
    [Fact]
    public void Letting_it_go_stops_the_clock()
    {
        var paper = new Paper();
        var block = new ControlTemplateBlock();

        var hints = new HintClock();

        _ = new ControlTemplatesOnDisc(paper, block, hints);

        hints.Dispose();

        int asked = paper.Asked;

        block.Templates.Add(Template("nanoKONTROL2", "machine.d"));
        block.Moved();

        Thread.Sleep(500);

        Assert.Equal(asked, paper.Asked);
    }
}
