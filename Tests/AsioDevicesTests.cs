using JingleBox2.Audio;
using JingleBox2.Audio.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The ASIO seam on a machine that has no ASIO, which is every machine these tests run on.
/// </summary>
/// <remarks>
/// That is not a gap in the testing, it is the case worth pinning. A missing native library
/// throws on the first call into it rather than when the assembly loads, so every one of these
/// answers has to come back as a plain no instead of taking the settings page down with it. CI
/// runs on Linux and on Windows and neither has <c>bassasio</c>, so both prove the same thing:
/// asking is safe.
/// </remarks>
public class AsioDevicesTests
{
    /// <summary>The seam under test.</summary>
    private readonly AsioDevices _asio = new();

    /// <summary>With no library there, it says so rather than throwing.</summary>
    [Fact]
    public void With_no_library_it_says_so()
    {
        Assert.False(_asio.Present);
        Assert.NotEmpty(_asio.Missing);
    }

    /// <summary>And it says so the same way every time it is asked.</summary>
    /// <remarks>
    /// Remembered rather than asked again, since asking costs a thrown exception each time and
    /// the answer cannot change while the program runs.
    /// </remarks>
    [Fact]
    public void The_answer_does_not_change_under_it()
    {
        bool first = _asio.Present;

        for (int again = 0; again < 5; again++) Assert.Equal(first, _asio.Present);
    }

    /// <summary>The list is empty rather than an error.</summary>
    [Fact]
    public void There_are_no_drivers_to_list()
    {
        Assert.Empty(_asio.Devices);
    }

    /// <summary>Opening one is refused, and nothing throws.</summary>
    [Fact]
    public void Opening_one_is_refused()
    {
        Assert.False(_asio.Open(0, 12345, 48000));
        Assert.False(_asio.Open(-1, 12345, 48000));
        Assert.False(_asio.Open(0, 0, 48000));
        Assert.False(_asio.Open(0, 12345, 0));
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

        _asio.Open(0, 12345, 48000);

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

    /// <summary>Every driver it could list would be marked as one.</summary>
    /// <remarks>
    /// Vacuous where there are none, and deliberately so: it is the assertion that fails first on
    /// the day somebody runs the suite on a machine that does have ASIO, which is the only place
    /// the real path can ever be exercised.
    /// </remarks>
    [Fact]
    public void Anything_listed_is_marked_as_asio()
    {
        Assert.All(_asio.Devices, one =>
        {
            Assert.Equal(AudioOutputKind.Asio, one.Kind);
            Assert.True(one.Id >= new AudioOutputs().AsioFrom);
            Assert.NotEmpty(one.Name);
            Assert.Contains("(ASIO)", one.ToString());
        });
    }
}
