using System;
using System.Collections.Generic;
using JingleBox2.Audio;
using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Finding the output somebody chose, on a machine whose list has moved since.
/// </summary>
/// <remarks>
/// **A device's number is a place in a list and the list moves.** An interface plugged in or
/// taken away shifts every row after it, so the number that was written down goes on naming a row
/// and stops naming the same device. It happened in one evening and nothing said so: at 19:47 the
/// twelfth row was the sound server and the thirteenth the compatibility layer over it, and an
/// hour later the twelfth row was the compatibility layer. The setting had said twelve all along.
///
/// From a chair that is a machine that quietly plays out of somewhere else, which is the worst
/// shape of fault there is, since from inside the application everything succeeded.
/// </remarks>
public sealed class OutputChoiceTests
{
    /// <summary>The rule under test. Holds nothing, so one serves every test here.</summary>
    private readonly IOutputChoice _choice = new OutputChoice();

    /// <summary>The list as it was the first time, read off a real machine's log.</summary>
    private static readonly IReadOnlyList<AudioOutput> Before = new[]
    {
        new AudioOutput(0, "No sound"),
        new AudioOutput(1, "Default"),
        new AudioOutput(6, "Steinberg UR44: USB Audio"),
        new AudioOutput(12, "PipeWire Sound Server"),
        new AudioOutput(13, "PulseAudio Sound Server")
    };

    /// <summary>And after the interface went away, which moved everything after it.</summary>
    private static readonly IReadOnlyList<AudioOutput> After = new[]
    {
        new AudioOutput(0, "No sound"),
        new AudioOutput(1, "Default"),
        new AudioOutput(11, "PipeWire Sound Server"),
        new AudioOutput(12, "PulseAudio Sound Server")
    };

    /// <summary>
    /// A device whose number has moved is found by its name.
    /// </summary>
    /// <remarks>
    /// The whole of it. Twelve meant the sound server when it was written down and means the
    /// compatibility layer over it now, and the name is what tells them apart.
    /// </remarks>
    [Fact]
    public void A_device_that_moved_is_found_by_its_name()
    {
        var found = _choice.Among(After, "PipeWire Sound Server", id: 12);

        Assert.NotNull(found);
        Assert.Equal("PipeWire Sound Server", found!.Name);
        Assert.Equal(11, found.Id);
    }

    /// <summary>And the number it now has is not the one that was stored.</summary>
    /// <remarks>
    /// Said as its own test, since a rule that answered the right name at the wrong row would
    /// pass the one above and still open the wrong device.
    /// </remarks>
    [Fact]
    public void The_number_it_now_has_is_the_one_that_is_used()
    {
        Assert.NotEqual(12, _choice.Among(After, "PipeWire Sound Server", id: 12)!.Id);
    }

    /// <summary>On a machine that has not changed, both questions answer the same device.</summary>
    [Fact]
    public void Nothing_moves_where_nothing_changed()
    {
        var found = _choice.Among(Before, "PipeWire Sound Server", id: 12);

        Assert.Equal(12, found!.Id);
    }

    /// <summary>
    /// A settings file with no name in it is answered by the number, exactly as before.
    /// </summary>
    /// <remarks>
    /// Every file written before the name existed, which is everybody's. It must go on meaning
    /// what it meant rather than falling through to whatever is first.
    /// </remarks>
    [Fact]
    public void A_file_with_no_name_is_answered_by_the_number()
    {
        var found = _choice.Among(Before, name: null, id: 13);

        Assert.Equal("PulseAudio Sound Server", found!.Name);
    }

    /// <summary>The name is asked first, so a stale number cannot overrule it.</summary>
    /// <remarks>
    /// The two disagree exactly when the list has moved, which is the case this exists for: the
    /// name is right and the number is a row somebody else is sitting in now.
    /// </remarks>
    [Fact]
    public void The_name_is_asked_before_the_number()
    {
        var found = _choice.Among(After, "PulseAudio Sound Server", id: 11);

        Assert.Equal("PulseAudio Sound Server", found!.Name);
    }

    /// <summary>A device that is not on this machine at all is nothing rather than a guess.</summary>
    /// <remarks>
    /// Which is an interface somebody has unplugged. The caller falls back on what the machine
    /// does offer; answering the nearest row instead would be this choosing a device on their
    /// behalf and saying nothing.
    /// </remarks>
    [Fact]
    public void A_device_that_is_not_here_is_nothing()
    {
        Assert.Null(_choice.Among(After, "Steinberg UR44: USB Audio", id: 6));
    }

    /// <summary>Read forgivingly, since a name is somebody else's string.</summary>
    [Fact]
    public void The_name_is_read_forgivingly()
    {
        Assert.Equal(11, _choice.Among(After, "  pipewire sound server ", id: -1)!.Id);
    }

    /// <summary>Nothing stored at all is nothing found, which is a fresh installation.</summary>
    [Fact]
    public void Nothing_stored_is_nothing_found()
    {
        Assert.Null(_choice.Among(Before, name: null, id: -1));
        Assert.Null(_choice.Among(Before, name: "", id: -1));
    }

    /// <summary>An empty machine is an empty answer rather than a fault.</summary>
    [Fact]
    public void An_empty_machine_is_answered_emptily()
    {
        Assert.Null(_choice.Among(Array.Empty<AudioOutput>(), "PipeWire Sound Server", id: 12));
        Assert.Null(_choice.Among(null, "PipeWire Sound Server", id: 12));
    }

    /// <summary>A driver is matched by its own name and not by what a picker draws.</summary>
    /// <remarks>
    /// The list shows a driver with the word ASIO on the end so it can be told from a system
    /// endpoint of the same name, and what is stored is the device's own name. Matching the drawn
    /// one would miss it every time.
    /// </remarks>
    [Fact]
    public void A_driver_is_matched_by_its_own_name()
    {
        var drivers = new[] { new AudioOutput(1000, "Some Card", AudioOutputKind.Asio) };

        Assert.Equal(1000, _choice.Among(drivers, "Some Card", id: -1)!.Id);
    }
}
