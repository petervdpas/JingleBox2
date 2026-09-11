using System.Collections.Generic;
using JingleBox2.Audio;
using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which of the three ways out the mix takes, asked without a driver, a graph or a card.
/// </summary>
/// <remarks>
/// **This is the rule that was wrong and could not be asked.** Who takes the mix out was two
/// booleans worked out in the middle of opening the output, so the only way to find out what a
/// machine would do was to run on that machine and listen. What it got wrong was the quietest
/// case there is: the output nobody has picked yet, which is the system's own default, on a
/// machine whose sound server was waiting to pull. The mix went out through the library instead
/// and everything still made a noise, so nothing looked broken.
/// </remarks>
public sealed class MixOutletTests
{
    /// <summary>Drivers that are there or not, and remember what they were asked.</summary>
    private sealed class Drivers : IAsioDevices
    {
        /// <summary>Which driver was opened, or minus one where none was.</summary>
        public int Opened { get; private set; } = -1;

        /// <summary>How many times they were let go of.</summary>
        public int Closed { get; private set; }

        /// <inheritdoc/>
        public bool Present => true;

        /// <inheritdoc/>
        public string Missing => "";

        /// <inheritdoc/>
        public IReadOnlyList<AudioOutput> Devices => new List<AudioOutput>();

        /// <inheritdoc/>
        public bool Open(int index, int stream, int rate)
        {
            Opened = index;

            return true;
        }

        /// <inheritdoc/>
        public void Close() => Closed++;

        /// <inheritdoc/>
        public int Latency => 0;

        /// <inheritdoc/>
        public int Frames => 0;

        /// <inheritdoc/>
        public int Rate => 0;
    }

    /// <summary>A graph node that is there or not.</summary>
    private sealed class Graph : IPipeWireOutput
    {
        /// <summary>Takes whether the machine has a server at all.</summary>
        /// <param name="present">Whether it is there.</param>
        public Graph(bool present = true) => Present = present;

        /// <summary>Whether it was asked to take the mix.</summary>
        public bool Took { get; private set; }

        /// <summary>How many times it was let go of.</summary>
        public int Closed { get; private set; }

        /// <inheritdoc/>
        public bool Present { get; }

        /// <inheritdoc/>
        public string Missing => Present ? "" : "there is no sound server here";

        /// <inheritdoc/>
        public bool IsOpen => Took;

        /// <inheritdoc/>
        public bool Open(int stream, int rate)
        {
            Took = true;

            return true;
        }

        /// <inheritdoc/>
        public void Close() => Closed++;
    }

    /// <summary>What the library calls the server's own row.</summary>
    private const string Server = "PipeWire Sound Server";

    /// <summary>Something else on the list, which is nobody's server.</summary>
    private const string Card = "HDA Intel PCH";

    /// <summary>The rule over a machine with both a driver and a server.</summary>
    /// <param name="graph">Whether the machine has a server.</param>
    /// <param name="drivers">The drivers, so a test can read what they were asked.</param>
    private static IMixOutlets Machine(Graph graph, Drivers drivers) =>
        new MixOutlets(drivers, graph, new SoundServerOutput());

    /// <summary>A driver named in the settings pulls the mix.</summary>
    [Fact]
    public void A_driver_pulls()
    {
        var outlet = Machine(new Graph(), new Drivers()).For(AudioOutputKind.Asio, 2, Card, false);

        Assert.True(outlet.Pulls);
        Assert.Equal("the driver", outlet.Word);
    }

    /// <summary>The server's own row pulls the mix, by the name the library gives it.</summary>
    [Fact]
    public void The_server_by_name_pulls()
    {
        var outlet = Machine(new Graph(), new Drivers()).For(AudioOutputKind.System, 12, Server, false);

        Assert.True(outlet.Pulls);
        Assert.Equal("the sound server", outlet.Word);
    }

    /// <summary>
    /// The system's own default pulls too, which is the case that was silently wrong.
    /// </summary>
    /// <remarks>
    /// A settings file that has never had an output picked holds the default, and where a server
    /// is running it owns what everything on the machine calls the default. So this is the
    /// ordinary state of a fresh installation rather than a corner of the rule.
    /// </remarks>
    [Fact]
    public void The_system_default_pulls_where_there_is_a_server()
    {
        var outlet = Machine(new Graph(), new Drivers()).For(AudioOutputKind.System, 0, Card, true);

        Assert.True(outlet.Pulls);
        Assert.Equal("the sound server", outlet.Word);
    }

    /// <summary>A card picked by name on a served machine is played by the library.</summary>
    /// <remarks>
    /// Somebody who picks the card rather than the server means the card, and the library is what
    /// reaches it. The server being there decides nothing on its own.
    /// </remarks>
    [Fact]
    public void A_card_picked_by_name_is_played_by_the_library()
    {
        var outlet = Machine(new Graph(), new Drivers()).For(AudioOutputKind.System, 3, Card, false);

        Assert.False(outlet.Pulls);
        Assert.Equal("", outlet.Word);
    }

    /// <summary>With no server on the machine, the library plays it whatever the row is called.</summary>
    [Fact]
    public void With_no_server_the_library_plays_it()
    {
        var outlet = Machine(new Graph(present: false), new Drivers())
            .For(AudioOutputKind.System, 12, Server, true);

        Assert.False(outlet.Pulls);
    }

    /// <summary>
    /// A driver wins over a server, since it was named in the settings and the server was not.
    /// </summary>
    /// <remarks>
    /// The two are not exclusive: a machine can have a driver installed and a default output at
    /// the same time, and asked in the other order every driver on such a machine would be
    /// ignored.
    /// </remarks>
    [Fact]
    public void A_driver_wins_over_a_server()
    {
        var outlet = Machine(new Graph(), new Drivers()).For(AudioOutputKind.Asio, 1, Server, true);

        Assert.Equal("the driver", outlet.Word);
    }

    /// <summary>The driver's own number reaches the driver, which is what it is held for.</summary>
    [Fact]
    public void The_drivers_own_number_is_what_is_opened()
    {
        var drivers = new Drivers();

        Machine(new Graph(), drivers).For(AudioOutputKind.Asio, 2, Card, false).Open(77, 44100);

        Assert.Equal(2, drivers.Opened);
    }

    /// <summary>Letting an outlet go lets go of the thing behind it.</summary>
    [Fact]
    public void Letting_it_go_reaches_what_is_behind_it()
    {
        var graph = new Graph();

        Machine(graph, new Drivers()).For(AudioOutputKind.System, 0, Server, false).Close();

        Assert.Equal(1, graph.Closed);
    }

    /// <summary>The library's outlet has nothing to let go of and says so by doing nothing.</summary>
    /// <remarks>
    /// The mix stops when the bus is freed and the bus belongs to whoever made it, so there is
    /// nothing here to close. It is an outlet like the other two all the same, which is what
    /// leaves one branch where there were three.
    /// </remarks>
    [Fact]
    public void The_library_has_nothing_to_let_go_of()
    {
        var outlet = new LibraryOutlet();

        outlet.Close();

        Assert.False(outlet.Pulls);
    }
}
