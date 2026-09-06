using JingleBox2.Config;
using JingleBox2.Config.Interfaces;
using JingleBox2.Config.Records;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where the patchbay's blocks were left, kept between one run and the next.
/// </summary>
/// <remarks>
/// Only what somebody moved is written down, which is the rule the shortcut map already keeps
/// and for the same reason: a default that is not on anybody's disc can still be improved.
///
/// The unhappy half is most of this, and all of it is real. A settings file is a file: it can be
/// edited by hand, written by a version that went wrong, or carried from another machine where
/// the window was a different size. What a stored place has to survive is being read back and
/// drawn at.
/// </remarks>
public class PatchPlacesTests
{
    /// <summary>A settings store that writes to nothing and counts what it was asked to write.</summary>
    private sealed class Counting : IConfigStore
    {
        /// <summary>How many times the settings were written out.</summary>
        public int Saved { get; private set; }

        /// <inheritdoc/>
        public string ConfigPath => "";

        /// <inheritdoc/>
        public AppConfig LoadOrCreateDefault() => new();

        /// <inheritdoc/>
        public void Save(AppConfig cfg) => Saved++;
    }

    /// <summary>A fresh pair, with nothing remembered.</summary>
    private static (IPatchPlaces Places, AppConfig Config, Counting Store) Bench()
    {
        var cfg = new AppConfig();
        var store = new Counting();

        return (new PatchPlaces(cfg, store), cfg, store);
    }

    /// <summary>A block nobody has moved has no place, and the graph's own stands.</summary>
    [Fact]
    public void A_block_nobody_moved_has_no_place()
    {
        var (places, _, _) = Bench();

        Assert.False(places.Placed("mixer", out _, out _));
    }

    /// <summary>Where a block was left is where it is found again.</summary>
    [Fact]
    public void Where_it_was_left_is_where_it_is_found()
    {
        var (places, _, _) = Bench();

        places.Place("mixer", 120, 340);

        Assert.True(places.Placed("mixer", out double x, out double y));
        Assert.Equal(120, x);
        Assert.Equal(340, y);
    }

    /// <summary>Moving it again writes over the same entry rather than adding a second.</summary>
    /// <remarks>
    /// A file that grew a line every time somebody dragged a block would be a settings file
    /// nobody could read after an afternoon, and the last entry would be the only one that meant
    /// anything.
    /// </remarks>
    [Fact]
    public void Moving_it_again_writes_over_the_same_entry()
    {
        var (places, cfg, _) = Bench();

        places.Place("mixer", 10, 10);
        places.Place("mixer", 40, 60);

        Assert.Single(cfg.PatchbayPlaces);
        Assert.True(places.Placed("mixer", out double x, out double y));
        Assert.Equal(40, x);
        Assert.Equal(60, y);
    }

    /// <summary>Each block keeps its own place.</summary>
    [Fact]
    public void Every_block_keeps_its_own()
    {
        var (places, cfg, _) = Bench();

        places.Place("mixer", 10, 10);
        places.Place("fire", 90, 20);

        Assert.Equal(2, cfg.PatchbayPlaces.Count);
        Assert.True(places.Placed("fire", out double x, out _));
        Assert.Equal(90, x);
    }

    /// <summary>Leaving a block where it already was writes nothing.</summary>
    /// <remarks>
    /// A press that picks a block out to read its details, and a drag that ends where it began,
    /// are the same gesture to a hand and neither is a change. Writing there would put the
    /// settings file on the disc every time somebody looked at a block.
    /// </remarks>
    [Fact]
    public void Leaving_it_where_it_was_writes_nothing()
    {
        var (places, _, store) = Bench();

        places.Place("mixer", 10, 10);

        int written = store.Saved;

        places.Place("mixer", 10, 10);
        places.Place("mixer", 10.2, 9.8);

        Assert.Equal(written, store.Saved);
    }

    /// <summary>A place that is not a real number is refused rather than stored.</summary>
    /// <remarks>
    /// A block at NaN is a block nobody can find, drawn nowhere, with nothing on the screen to
    /// say why. It cannot come from the surface, which clamps every drag to the page; it can
    /// come from a file.
    /// </remarks>
    [Fact]
    public void A_place_that_is_not_a_number_is_refused()
    {
        var (places, cfg, _) = Bench();

        places.Place("mixer", double.NaN, 20);
        places.Place("fire", 20, double.PositiveInfinity);

        Assert.Empty(cfg.PatchbayPlaces);
    }

    /// <summary>And one already in the file is passed over rather than drawn at.</summary>
    [Fact]
    public void A_stored_place_that_is_not_a_number_is_passed_over()
    {
        var (places, cfg, _) = Bench();

        cfg.PatchbayPlaces.Add(new PatchbayPlace { Node = "mixer", X = double.NaN, Y = 10 });

        Assert.False(places.Placed("mixer", out _, out _));
    }

    /// <summary>A block with no address is neither stored nor looked up.</summary>
    [Fact]
    public void A_block_with_no_address_is_left_alone()
    {
        var (places, cfg, _) = Bench();

        places.Place("", 10, 10);

        Assert.Empty(cfg.PatchbayPlaces);
        Assert.False(places.Placed("", out _, out _));
    }

    /// <summary>A place off the top left is kept, since a window can be resized.</summary>
    /// <remarks>
    /// Deliberately not clamped here. Where the page ends is the surface's business and it
    /// changes with the window; a place trimmed on the way in would quietly move somebody's
    /// arrangement every time they opened the application in a smaller window.
    /// </remarks>
    [Fact]
    public void A_place_off_the_page_is_still_kept()
    {
        var (places, _, _) = Bench();

        places.Place("mixer", -40, 5000);

        Assert.True(places.Placed("mixer", out double x, out double y));
        Assert.Equal(-40, x);
        Assert.Equal(5000, y);
    }

    /// <summary>The addresses are compared exactly, since a node name is an address.</summary>
    [Fact]
    public void Addresses_are_compared_as_they_are_written()
    {
        var (places, _, _) = Bench();

        places.Place("Firefox", 10, 10);

        Assert.False(places.Placed("firefox", out _, out _));
    }

    /// <summary>What is written down is the one entry, with the block's own address on it.</summary>
    [Fact]
    public void What_is_written_names_the_block()
    {
        var (places, cfg, _) = Bench();

        places.Place("alsa_output.pci-0000_00_1f.3.analog-stereo", 12, 34);

        var place = Assert.Single(cfg.PatchbayPlaces);

        Assert.Equal("alsa_output.pci-0000_00_1f.3.analog-stereo", place.Node);
        Assert.Equal(12, place.X);
        Assert.Equal(34, place.Y);
    }
}
