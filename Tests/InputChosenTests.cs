using System;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Audio.Routing.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which source the IN strip opens on, and whether it is being heard when it does.
/// </summary>
/// <remarks>
/// **Audio goes in as well as out, and only the output half was being read.** The first source was
/// whatever an output happened to be playing, whichever input somebody had chosen in the settings,
/// so a machine set up to record a microphone opened on the desktop's own playback. There is a
/// setting two pages away that exists to answer exactly this and nothing asked it.
/// </remarks>
public class InputChosenTests
{
    /// <summary>
    /// Reads the routes and waits for the strip to land on one.
    /// </summary>
    /// <remarks>
    /// <c>RefreshRoutes</c> is <c>async void</c> and reads the machine off the pool, so what it
    /// settles on cannot be read on the line after asking for it. Waited for rather than slept
    /// through, so a slow machine takes longer rather than answering wrongly.
    /// </remarks>
    /// <param name="bench">The page and its fakes.</param>
    /// <param name="wanted">What it should land on.</param>
    private static AudioRoute? Opened(RecorderBench bench, AudioRoute wanted)
    {
        bench.Page.RefreshRoutes();

        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (Equals(bench.Page.SelectedRoute, wanted)) break;

            Thread.Sleep(2);
        }

        return bench.Page.SelectedRoute;
    }

    /// <summary>The input named in the settings is what the strip opens on.</summary>
    [Fact]
    public void The_input_in_the_settings_is_what_it_opens_on()
    {
        var bench = new RecorderBench();

        bench.Settings.RecordInputDevice = "Microphone";

        Assert.Equal(RecorderBench.Microphone, Opened(bench, RecorderBench.Microphone));
    }

    /// <summary>Case and stray spacing do not stop it being found.</summary>
    /// <remarks>
    /// The name is stored as the machine spelled it on the day it was chosen, and the same device
    /// can come back spelled differently after a replug or a driver change.
    /// </remarks>
    [Fact]
    public void Case_and_spacing_do_not_stop_it()
    {
        var bench = new RecorderBench();

        bench.Settings.RecordInputDevice = "  microphone ";

        Assert.Equal(RecorderBench.Microphone, Opened(bench, RecorderBench.Microphone));
    }

    /// <summary>
    /// With no input chosen it still opens on what an output is playing, which is what it did.
    /// </summary>
    /// <remarks>
    /// The ordinary answer on a machine nobody has set up, and it is the one this replaced rather
    /// than removed: recording the desktop is what most people want the first time they look.
    /// </remarks>
    [Fact]
    public void With_no_input_chosen_it_still_opens_on_what_is_playing()
    {
        var bench = new RecorderBench();

        Assert.Equal(RecorderBench.Speakers, Opened(bench, RecorderBench.Speakers));
    }

    /// <summary>An input that is named and not there falls back rather than opening on nothing.</summary>
    [Fact]
    public void An_input_that_is_gone_falls_back()
    {
        var bench = new RecorderBench();

        bench.Settings.RecordInputDevice = "A microphone somebody unplugged";

        Assert.Equal(RecorderBench.Speakers, Opened(bench, RecorderBench.Speakers));
    }

    /// <summary>
    /// **And it is never being heard on the way in.**
    /// </summary>
    /// <remarks>
    /// Nothing stores it and nothing turns it on, which is how it should stay: a machine that
    /// opened already listening would put a microphone into the speakers before anybody had asked
    /// for anything, and on the machine this happens to that is a room full of it at whatever the
    /// master was left at.
    ///
    /// Pinned rather than fixed, since it is already true. It is the kind of thing that would be
    /// broken by somebody storing the switch for what looks like a convenience.
    /// </remarks>
    [Fact]
    public void Hearing_is_off_when_the_page_opens()
    {
        var bench = new RecorderBench();

        Assert.False(bench.Page.Hearing);

        bench.Settings.RecordInputDevice = "Microphone";

        Opened(bench, RecorderBench.Microphone);

        Assert.False(bench.Page.Hearing);
    }
}
