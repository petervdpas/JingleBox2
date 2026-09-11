using System;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The machine following what the pages asked for, and going on following it.
/// </summary>
/// <remarks>
/// Reported off a show: the input set to a browser with Hear it off, which is the quiet setting
/// somebody uses to line a source up before they need it. Change tab and the browser was heard on
/// the speakers a second later.
///
/// Two things were wrong and both are here. Choosing a source registered no reason to keep
/// watching the arrangement, since only the tick box told the page that anything was standing;
/// and the holding that puts back what the machine's session manager has re-wired lived inside a
/// reading that a page starts when it is drawn and stops when it is put away. **Which page is on
/// screen may not decide what the machine is wired to.**
/// </remarks>
public sealed class InputArrangementTests
{
    /// <summary>An input path that says what it is holding and counts what was asked of it.</summary>
    private sealed class Pointed : IInputPath
    {
        /// <inheritdoc/>
        public AudioRoute? Source { get; private set; }

        /// <inheritdoc/>
        public bool Heard { get; private set; }

        /// <inheritdoc/>
        public bool Aside => Source != null;

        /// <summary>How many arrangements were asked for.</summary>
        public int Arranged { get; private set; }

        /// <summary>And how many times the arrangement was held.</summary>
        public int Held { get; private set; }

        /// <summary>What the next hold answers, which is what a source creeping back looks like.</summary>
        public bool CreptBack { get; set; }

        /// <summary>Whether the graph throws, which it is entitled to do at any moment.</summary>
        public bool Throws { get; set; }

        /// <inheritdoc/>
        public bool CanHear(AudioRoute? source, string? playingOut) => true;

        /// <inheritdoc/>
        public InputAside Set(AudioRoute? source, bool heard, string? playingOut)
        {
            Arranged++;

            Source = source;
            Heard = heard;

            return source == null ? InputAside.Nothing : InputAside.Moved;
        }

        /// <inheritdoc/>
        public bool Hold()
        {
            Held++;

            if (Throws) throw new InvalidOperationException("the graph said no");

            return CreptBack;
        }

        /// <inheritdoc/>
        public void GiveBack() => Source = null;
    }

    /// <summary>A source of the kind somebody lines up before a show.</summary>
    private static readonly AudioRoute Browser = new("Firefox", "Firefox", AudioRouteKind.Application);

    /// <summary>The same source read off the graph again, which is a different object.</summary>
    private static readonly AudioRoute Again = new("Firefox", "Firefox", AudioRouteKind.Application);

    /// <summary>A setting said is a machine arranged, without anybody asking it to be.</summary>
    [Fact]
    public void Saying_what_is_wanted_arranges_it()
    {
        var input = new Pointed();
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        setting.Say(Browser, heard: false, playingOut: "Speakers");

        Assert.Equal(1, input.Arranged);
        Assert.Equal(InputAside.Moved, arrangement.Aside);
    }

    /// <summary>
    /// **The same setting said again rewires nothing.**
    /// </summary>
    /// <remarks>
    /// The graph is read every couple of seconds and the route objects are new every time, so a
    /// comparison by object would read every reading as somebody choosing the source again and
    /// take the links out and put them back, which is audible.
    /// </remarks>
    [Fact]
    public void The_same_setting_said_again_moves_nothing()
    {
        var input = new Pointed();
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        setting.Say(Browser, heard: false, playingOut: "Speakers");
        setting.Say(Again, heard: false, playingOut: "Speakers");
        setting.Say(Again, heard: false, playingOut: "Speakers");

        Assert.Equal(1, input.Arranged);
    }

    /// <summary>And a real change is a real arrangement.</summary>
    [Fact]
    public void Hearing_it_is_a_change()
    {
        var input = new Pointed();
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        setting.Say(Browser, heard: false, playingOut: "Speakers");
        setting.Say(Browser, heard: true, playingOut: "Speakers");

        Assert.Equal(2, input.Arranged);
        Assert.True(input.Heard);
    }

    /// <summary>
    /// **A source that is not being heard is still held off its own output.**
    /// </summary>
    /// <remarks>
    /// The case this was written for. Hear it decides what the desk does with the source; being
    /// aside is what stops it playing out of its own speakers, and the two are different
    /// questions.
    /// </remarks>
    [Fact]
    public void A_source_that_is_not_being_heard_is_still_held()
    {
        var input = new Pointed();
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        setting.Say(Browser, heard: false, playingOut: "Speakers");

        arrangement.Check();
        arrangement.Check();

        Assert.Equal(2, input.Held);
    }

    /// <summary>Nothing chosen is nothing to hold, which is every ordinary session.</summary>
    [Fact]
    public void With_nothing_aside_it_asks_for_nothing()
    {
        var input = new Pointed();
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        arrangement.Check();

        Assert.Equal(0, input.Held);
    }

    /// <summary>A source given back is left alone.</summary>
    [Fact]
    public void A_source_given_back_is_left_alone()
    {
        var input = new Pointed();
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        setting.Say(Browser, heard: false, playingOut: "Speakers");

        arrangement.Check();

        setting.Say(null, heard: false, playingOut: "Speakers");

        arrangement.Check();

        Assert.Equal(1, input.Held);
    }

    /// <summary>A graph that answers badly costs one look rather than the application.</summary>
    /// <remarks>
    /// It runs on a clock of its own with nobody watching, so anything thrown here would be the
    /// process gone with nothing on the screen to say why.
    /// </remarks>
    [Fact]
    public void A_graph_that_throws_costs_one_look()
    {
        var input = new Pointed { Throws = true };
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        setting.Say(Browser, heard: false, playingOut: "Speakers");

        arrangement.Check();
        arrangement.Check();

        Assert.Equal(2, input.Held);
    }

    /// <summary>
    /// **A source that crept back is said out loud**, since the machine is undoing the
    /// arrangement and that is worth seeing rather than finding out about afterwards.
    /// </summary>
    [Fact]
    public void A_source_that_crept_back_is_said_out_loud()
    {
        var input = new Pointed { CreptBack = true };
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        AudioRoute? said = null;

        arrangement.PutBack += source => said = source;

        setting.Say(Browser, heard: false, playingOut: "Speakers");

        arrangement.Check();

        Assert.Equal(Browser.Node, said?.Node);
    }

    /// <summary>And nothing is said where nothing had come back, which is every ordinary look.</summary>
    [Fact]
    public void A_source_that_stayed_put_is_not_mentioned()
    {
        var input = new Pointed();
        var setting = new InputSetting();

        using var arrangement = new InputArrangement(setting, input);

        int said = 0;

        arrangement.PutBack += _ => said++;

        setting.Say(Browser, heard: false, playingOut: "Speakers");

        arrangement.Check();
        arrangement.Check();

        Assert.Equal(0, said);
    }

    /// <summary>Nothing is asked of it once it has been let go of.</summary>
    [Fact]
    public void Letting_it_go_ends_the_watching()
    {
        var input = new Pointed();
        var setting = new InputSetting();

        var arrangement = new InputArrangement(setting, input);

        setting.Say(Browser, heard: false, playingOut: "Speakers");

        arrangement.Dispose();

        setting.Say(null, heard: true, playingOut: "Speakers");

        Assert.Equal(1, input.Arranged);
    }
}
