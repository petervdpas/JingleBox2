using JingleBox2.Audio;
using JingleBox2.Audio.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// One stored number naming a device out of two lists that both start at nought.
/// </summary>
/// <remarks>
/// The rule that matters most is the one about the past: every settings file on anybody's disc
/// holds a system device's own number, and it has to go on meaning that after ASIO exists.
/// Getting that wrong sends somebody's audio out of a driver they have never heard of, or out of
/// nothing at all.
/// </remarks>
public class AudioOutputNumberTests
{
    /// <summary>The rule under test. It holds nothing, so one is enough.</summary>
    private readonly AudioOutputs _outputs = new();

    /// <summary>A system device keeps the number it always had.</summary>
    [Fact]
    public void A_system_device_keeps_its_own_number()
    {
        for (int index = 0; index < 32; index++)
            Assert.Equal(index, _outputs.Numbered(AudioOutputKind.System, index));
    }

    /// <summary>And a number stored before ASIO existed still names that same device.</summary>
    [Fact]
    public void A_number_stored_before_asio_still_means_what_it_meant()
    {
        foreach (int stored in new[] { 0, 1, 2, 7, 31, 999 })
            Assert.Equal((AudioOutputKind.System, stored), _outputs.Which(stored));
    }

    /// <summary>An ASIO driver is lifted clear of the system's numbers.</summary>
    [Fact]
    public void An_asio_driver_is_lifted_clear()
    {
        Assert.Equal(_outputs.AsioFrom, _outputs.Numbered(AudioOutputKind.Asio, 0));
        Assert.Equal(_outputs.AsioFrom + 3, _outputs.Numbered(AudioOutputKind.Asio, 3));

        Assert.Equal((AudioOutputKind.Asio, 0), _outputs.Which(_outputs.AsioFrom));
        Assert.Equal((AudioOutputKind.Asio, 3), _outputs.Which(_outputs.AsioFrom + 3));
    }

    /// <summary>Every number goes out and comes back as itself, in both worlds.</summary>
    [Fact]
    public void A_number_goes_out_and_comes_back()
    {
        foreach (var kind in new[] { AudioOutputKind.System, AudioOutputKind.Asio })
            for (int index = 0; index < 64; index++)
            {
                var (was, back) = _outputs.Which(_outputs.Numbered(kind, index));

                Assert.Equal(kind, was);
                Assert.Equal(index, back);
            }
    }

    /// <summary>Nothing picked is the system's, which is what an unset setting holds.</summary>
    /// <remarks>
    /// Minus one is what the settings start at, and it must not come back as an ASIO driver at
    /// some negative index: that is a number nothing can be looked up by.
    /// </remarks>
    [Fact]
    public void Nothing_picked_is_the_systems()
    {
        Assert.Equal((AudioOutputKind.System, -1), _outputs.Which(-1));
        Assert.Equal(AudioOutputKind.System, _outputs.Which(int.MinValue).Kind);
    }

    /// <summary>An index below nought is taken as the first driver rather than reaching back.</summary>
    /// <remarks>
    /// Without this a negative index would number an ASIO driver below the boundary, which reads
    /// back as a system device, which is the one way this rule could send audio somewhere nobody
    /// asked for.
    /// </remarks>
    [Fact]
    public void A_driver_below_nought_cannot_reach_into_the_systems_numbers()
    {
        Assert.Equal(_outputs.AsioFrom, _outputs.Numbered(AudioOutputKind.Asio, -1));
        Assert.Equal(_outputs.AsioFrom, _outputs.Numbered(AudioOutputKind.Asio, -999));

        Assert.Equal(AudioOutputKind.Asio, _outputs.Which(_outputs.Numbered(AudioOutputKind.Asio, -5)).Kind);
    }
}
