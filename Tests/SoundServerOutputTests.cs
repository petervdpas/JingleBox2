using Xunit;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Tests;

/// <summary>
/// Which output means the sound server, and therefore which one takes the node's path.
/// </summary>
/// <remarks>
/// **Every entry the library lists on Linux is a real thing somebody might mean**, and one of
/// them is the one this application can be a proper node on. Picked, the mix goes out through the
/// server and appears on the graph as this application, two ports wide and named; picked
/// otherwise, everything goes the way it always did through the library's own device. So this
/// shortens no list and refuses nothing: it answers one question about one name, and the answer
/// chooses a path.
///
/// The list here is read off a real machine's log rather than invented, since the interesting
/// part is how alike the rows look: a card the server already holds, three conversion plugins, a
/// default, and two entries with the words "Sound Server" in them meaning different things.
/// </remarks>
public sealed class SoundServerOutputTests
{
    /// <summary>The rule under test. Holds nothing, so one serves every test here.</summary>
    private readonly ISoundServerOutput _server = new SoundServerOutput();

    /// <summary>The server's own entry is the server.</summary>
    [Fact]
    public void The_servers_own_entry_is_the_server()
    {
        Assert.True(_server.Is("PipeWire Sound Server", standard: false));
    }

    /// <summary>
    /// And so is whatever the system calls its default.
    /// </summary>
    /// <remarks>
    /// Where the server is running it owns what everything else on the machine calls the default
    /// output, so the two rows are two ways of reaching one thing and nobody should have to know
    /// which is which. The system says which is the default; this does not work it out.
    /// </remarks>
    [Fact]
    public void The_systems_own_default_is_the_server_too()
    {
        Assert.True(_server.Is("Default", standard: true));
        Assert.True(_server.Is("HDA Intel PCH: ALC257 Analog", standard: true));
    }

    /// <summary>A card is not, however plausibly it is named.</summary>
    /// <remarks>
    /// The one that matters, since it is the row somebody reaches for by instinct: their own
    /// interface, by its own name. Picked, it opens through the library as it always has.
    /// </remarks>
    [Fact]
    public void A_card_is_not_the_server()
    {
        Assert.False(_server.Is("Steinberg UR44: USB Audio", standard: false));
        Assert.False(_server.Is("HDA Intel PCH: ALC257 Analog", standard: false));
    }

    /// <summary>
    /// The other sound server is not it either.
    /// </summary>
    /// <remarks>
    /// Matched whole rather than by the words they share. <c>PulseAudio Sound Server</c> sits in
    /// the same list and is a compatibility layer over the same server, one hop further from the
    /// graph, so a fragment match would send the node's path at the wrong row.
    /// </remarks>
    [Fact]
    public void The_other_sound_server_is_not_it()
    {
        Assert.False(_server.Is("PulseAudio Sound Server", standard: false));
    }

    /// <summary>Nor is a conversion plugin, nor the device that plays nothing.</summary>
    [Fact]
    public void A_plugin_is_not_the_server()
    {
        Assert.False(_server.Is("Rate Converter Plugin Using Speex Resampler", standard: false));
        Assert.False(_server.Is("Plugin for channel upmix (4,6,8)", standard: false));
        Assert.False(_server.Is("No sound", standard: false));
    }

    /// <summary>Read forgivingly, since a description is somebody else's string.</summary>
    [Fact]
    public void The_name_is_read_forgivingly()
    {
        Assert.True(_server.Is("  pipewire sound server ", standard: false));
    }

    /// <summary>Nothing at all is not the server, which is what an unnamed device answers.</summary>
    [Fact]
    public void Nothing_is_not_the_server()
    {
        Assert.False(_server.Is(null, standard: false));
        Assert.False(_server.Is("", standard: false));
        Assert.False(_server.Is("   ", standard: false));
    }
}
