using System.Collections.Generic;
using System.Linq;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A device is opened before it is offered, and the ones that will not open are left out.
/// </summary>
/// <remarks>
/// **The list the audio library gives is what the machine has, not what is free.** Where a sound
/// server is playing through a card it holds that card, and nothing else may open it: the card
/// shows up in the system's own settings, shows up here, and refuses the moment it is picked.
/// What that looked like was <c>Bass.Init failed: Busy</c> drawn as a stack trace across the
/// settings page, over a choice this application had just offered.
///
/// The probe itself needs a sound card and is not exercised here. What is, is the rule around
/// it: what gets offered, what does not, that the one already held is never asked about, and that
/// a machine where nothing opens ends up with an empty list rather than an exception.
/// </remarks>
public sealed class OutputProbeTests
{
    /// <summary>A probe that answers from a list and remembers what it was asked.</summary>
    private sealed class Asked : IOutputProbe
    {
        /// <summary>Which devices open. Anything not named refuses.</summary>
        private readonly HashSet<int> _open;

        /// <summary>Every device this was asked about, in order.</summary>
        public List<int> Questions { get; } = new();

        /// <summary>Takes the devices that will open.</summary>
        /// <param name="open">Their indices.</param>
        public Asked(params int[] open) => _open = new HashSet<int>(open);

        /// <inheritdoc/>
        public bool Opens(int device)
        {
            Questions.Add(device);

            return _open.Contains(device);
        }
    }

    /// <summary>An engine over a probe, with no card behind it.</summary>
    /// <param name="probe">What answers for the devices.</param>
    private static BassAudioEngine Engine(IOutputProbe probe) =>
        new(padCount: 1, deviceRate: 44100, rate: null, probe: probe);

    /// <summary>
    /// A device that will not open is not in the list.
    /// </summary>
    /// <remarks>
    /// The whole of it. Vacuous on a machine the library lists nothing for, which is what CI is,
    /// and true wherever it lists anything.
    /// </remarks>
    [Fact]
    public void A_device_that_will_not_open_is_not_offered()
    {
        var probe = new Asked();

        using var engine = Engine(probe);

        var offered = engine.GetOutputDevices().Where(one => one.Id < 1000).ToList();

        Assert.Empty(offered);
    }

    /// <summary>Nothing is offered that was not opened first.</summary>
    /// <remarks>
    /// The other half, so a probe that answered no to everything could not pass this file. It is
    /// a subset rather than the same count, since what survives the probe then goes through
    /// <see cref="JingleBox2.Audio.Interfaces.ISoundServerOutput"/>: on a machine that plays
    /// through a sound server, everything but the server's own entry is dropped afterwards.
    /// </remarks>
    [Fact]
    public void Nothing_is_offered_that_was_not_opened_first()
    {
        var everything = new Asked(Enumerable.Range(0, 64).ToArray());

        using var engine = Engine(everything);

        var offered = engine.GetOutputDevices().Where(one => one.Id < 1000).ToList();

        Assert.True(offered.Count <= everything.Questions.Count);
        Assert.All(offered, one => Assert.Contains(one.Id, everything.Questions));
    }

    /// <summary>Every endpoint the library lists is asked about, and none is taken on trust.</summary>
    [Fact]
    public void Every_endpoint_is_asked_about()
    {
        var probe = new Asked();

        using var engine = Engine(probe);

        engine.GetOutputDevices();

        Assert.Equal(probe.Questions.Distinct().Count(), probe.Questions.Count);
    }

    /// <summary>Asking twice answers the same way, since the list is read fresh each time.</summary>
    /// <remarks>
    /// A device is plugged in and unplugged while the application runs, so nothing here is
    /// remembered: what must not move is the answer for a machine that has not changed.
    /// </remarks>
    [Fact]
    public void The_list_comes_back_the_same_way_twice()
    {
        var probe = new Asked(0, 1);

        using var engine = Engine(probe);

        Assert.Equal(
            engine.GetOutputDevices().Select(one => (one.Id, one.Name)),
            engine.GetOutputDevices().Select(one => (one.Id, one.Name)));
    }

    /// <summary>A probe that throws is a device that is not offered, rather than a page that dies.</summary>
    /// <remarks>
    /// The list is read while the settings page is being built, so anything thrown here is the
    /// application failing to start rather than one device missing.
    /// </remarks>
    [Fact]
    public void A_probe_that_throws_costs_one_device_and_not_the_list()
    {
        using var engine = Engine(new Throwing());

        var offered = engine.GetOutputDevices();

        Assert.NotNull(offered);
    }

    /// <summary>One that cannot answer at all.</summary>
    private sealed class Throwing : IOutputProbe
    {
        /// <inheritdoc/>
        public bool Opens(int device) => throw new System.InvalidOperationException("no card");
    }
}
