using System.Linq;
using JingleBox2.Audio;
using JingleBox2.Audio.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The ASIO seam, on a machine that has it and on one that has not.
/// </summary>
/// <remarks>
/// **This file used to say there was no ASIO on any machine the suite runs on, and three of its
/// assertions were built on that.** It was not true: the machine it was written on has three
/// drivers installed. What made it look true is that <c>bassasio.dll</c> was copied beside the
/// application and never beside the tests, so the first call into it threw, the seam reported
/// no, and the tests agreed with it. **A test that reads as a fact about the code and is really
/// a fact about a missing copy step is worse than no test**, since it reports green for the rest
/// of its life and nobody looks again. The library is carried into this project's own output now
/// and the claims below are split by what is actually there.
///
/// **What none of it does is claim how many drivers there are.** That was the second version of
/// this file's mistake and it lasted about ten minutes: a test asserting the list was not empty
/// whenever the library reported itself present. The library loading and a driver being
/// installed are two independent facts, and <c>bassasio.dll</c> is checked into this repository,
/// so **every Windows machine that clones it reports present and most of them have no drivers at
/// all**. It would have passed here and failed for everybody else, which is worse than the fault
/// it was written to replace. What is asserted instead holds at nought drivers and at three, and
/// what running it here buys is those same words meeting three real ones instead of an empty
/// list.
///
/// So there are two kinds of test here. What is true whatever is installed, which is all but one
/// of them. And what is true only without the library, which is the case CI runs on Linux and
/// the one the settings page has to survive: a missing native throws on the first call into it
/// rather than when the assembly loads, so every answer has to come back as a plain no instead
/// of taking the page down.
///
/// **Nothing here opens a driver, and that is deliberate rather than shy.** <c>Open</c> calls the
/// library's own init before it can fail on anything else, and an ASIO driver is normally
/// exclusive, so opening one from a test takes the card off whatever had it. The old file called
/// <c>Open(0, 12345, 48000)</c> twice, which on a machine with drivers reached the hardware,
/// grabbed it and let it go again. What is exercised instead is every refusal that happens before
/// the driver is touched, which is the whole of what this seam decides for itself.
/// </remarks>
public class AsioDevicesTests
{
    /// <summary>The seam under test.</summary>
    private readonly AsioDevices _asio = new();

    /// <summary>How the application numbers an output, for the range a driver has to sit in.</summary>
    private readonly AudioOutputs _numbering = new();

    /// <summary>
    /// Whether it is here is said the same way every time it is asked.
    /// </summary>
    /// <remarks>
    /// Remembered rather than asked again, since asking costs a thrown exception where the answer
    /// is no and the answer cannot change while the program runs.
    /// </remarks>
    [Fact]
    public void The_answer_does_not_change_under_it()
    {
        bool first = _asio.Present;

        for (int again = 0; again < 5; again++) Assert.Equal(first, _asio.Present);
    }

    /// <summary>
    /// And whichever way it is, there is a sentence about it exactly when there needs to be.
    /// </summary>
    /// <remarks>
    /// The invariant rather than either half of it, so this says something on every machine: a
    /// reason to show is what being absent means, and having one while the library is there would
    /// be a settings page explaining away drivers it can see.
    /// </remarks>
    [Fact]
    public void There_is_a_reason_exactly_when_there_is_nothing_here()
    {
        Assert.Equal(_asio.Present, _asio.Missing.Length == 0);
    }

    /// <summary>Nonsense is refused, and refused before any driver is reached.</summary>
    /// <remarks>
    /// Every one of these is turned away by the argument guard, which sits above the library's
    /// own init, so this is safe to run on a machine whose card somebody else is using. A real
    /// index is deliberately not among them: see the remarks on the class.
    /// </remarks>
    [Fact]
    public void Nonsense_is_refused_before_a_driver_is_touched()
    {
        Assert.False(_asio.Open(-1, 12345, 48000));
        Assert.False(_asio.Open(0, 0, 48000));
        Assert.False(_asio.Open(0, 12345, 0));
        Assert.False(_asio.Open(-1, 0, 0));
    }

    /// <summary>With nothing open there is no block and no rate, rather than a leftover.</summary>
    /// <remarks>
    /// Both are the driver's own answers read back, so nought is the only honest thing to say
    /// where no driver has answered. A settings page reading either of these has to be able to
    /// tell "the card is on 256" from "there is no card".
    /// </remarks>
    [Fact]
    public void Nothing_open_has_no_block_and_no_rate()
    {
        Assert.Equal(0, _asio.Frames);
        Assert.Equal(0, _asio.Rate);

        _asio.Open(-1, 12345, 48000);

        Assert.Equal(0, _asio.Frames);
        Assert.Equal(0, _asio.Rate);

        _asio.Close();

        Assert.Equal(0, _asio.Frames);
        Assert.Equal(0, _asio.Rate);
    }

    /// <summary>Closing one that was never open is safe, however often.</summary>
    [Fact]
    public void Closing_what_was_never_open_is_safe()
    {
        _asio.Close();
        _asio.Close();

        Assert.Equal(0, _asio.Latency);
    }

    /// <summary>Anything it lists is marked and numbered as a driver.</summary>
    /// <remarks>
    /// True either way and vacuous without the library, which is what it was before and is worth
    /// keeping: the same words now hold against three real drivers rather than against nothing.
    /// </remarks>
    [Fact]
    public void Anything_listed_is_marked_as_asio()
    {
        Assert.All(_asio.Devices, one =>
        {
            Assert.Equal(AudioOutputKind.Asio, one.Kind);
            Assert.True(one.Id >= _numbering.AsioFrom);
            Assert.NotEmpty(one.Name);
            Assert.Contains("(ASIO)", one.ToString());
        });
    }

    /// <summary>Without the library there is nothing to list and nothing to open.</summary>
    /// <remarks>
    /// The case CI runs on both platforms, and the one that matters most: a machine with no ASIO
    /// is the ordinary machine, and what it must never do is fail rather than answer. Asserted
    /// rather than skipped, so it says something wherever it applies.
    /// </remarks>
    [Fact]
    public void Without_the_library_there_is_nothing_and_it_says_so()
    {
        if (_asio.Present) return;

        Assert.Empty(_asio.Devices);
        Assert.NotEmpty(_asio.Missing);
        Assert.False(_asio.Open(0, 12345, 48000));
    }

    /// <summary>
    /// Whatever is listed, no two of them share an id.
    /// </summary>
    /// <remarks>
    /// The picker stores one number and looks a driver up by it, so two drivers sharing an id is
    /// a settings file that opens the wrong card. The numbering that keeps them apart lifts ASIO
    /// clear of the system's own endpoints at <see cref="AudioOutputs.AsioFrom"/>.
    ///
    /// **No claim about how many there are, and that is the point.** This test was written for
    /// about ten minutes as "with the library the drivers are listed", asserting the list was not
    /// empty whenever <c>Present</c> was true. That is two different facts run together: the
    /// library loading and a driver being installed are independent, and <c>bassasio.dll</c> is
    /// checked in, so every Windows machine that clones this reports present and most of them have
    /// no drivers at all. Vacuous at nought drivers, true at three.
    /// </remarks>
    [Fact]
    public void No_two_drivers_share_an_id()
    {
        var drivers = _asio.Devices;

        Assert.Equal(drivers.Count, drivers.Select(one => one.Id).Distinct().Count());
    }

    /// <summary>Asking for the list twice answers the same drivers in the same order.</summary>
    /// <remarks>
    /// The list is read from the library each time rather than kept, since a card is plugged in
    /// and unplugged while the application runs. What must not move is the order, because the
    /// number stored in the settings is a place in it: a list that came back shuffled would open
    /// a different card than the one somebody chose.
    /// </remarks>
    [Fact]
    public void The_list_comes_back_the_same_way_twice()
    {
        Assert.Equal(
            _asio.Devices.Select(one => (one.Id, one.Name)),
            _asio.Devices.Select(one => (one.Id, one.Name)));
    }
}
