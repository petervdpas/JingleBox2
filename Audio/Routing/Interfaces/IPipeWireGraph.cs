using System.Collections.Generic;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing.Interfaces;

/// <summary>
/// Reads what the PipeWire tools print.
/// </summary>
/// <remarks>
/// Kept apart from running them, deliberately, so the parsing can be checked against real
/// output without a sound server in the room. What the tools print is the one part of this that
/// changes underneath us: a version of PipeWire that adds a column or moves an id breaks the
/// reading and nothing else, and that is a thing a test can hold still.
/// </remarks>
public interface IPipeWireGraph
{
    /// <summary>
    /// Reads one line of a port listing, or null when the line is not one.
    /// </summary>
    /// <remarks>
    /// A port line is "node:port". The node never contains a colon and the port often does
    /// not either, but a MIDI port can, so the first colon is the one that counts.
    ///
    /// The listing is asked for with an id in front when "pw-link -I" is used, so a leading run
    /// of digits followed by a space is dropped before the name is read. Anything else in front
    /// is left alone, which makes a line that is not a port fail the colon test and come back
    /// as null rather than as a port with a strange name.
    /// </remarks>
    PipeWirePort? ParsePort(string? line);

    /// <summary>
    /// Every port in a listing, in the order the tool printed them.
    /// </summary>
    /// <remarks>
    /// Lines that are not ports are dropped rather than reported: the tools print headings and
    /// blank lines, and a listing that has grown a line nobody expected should cost the line
    /// rather than the whole reading.
    /// </remarks>
    IReadOnlyList<PipeWirePort> ParsePorts(string? text);

    /// <summary>
    /// Reads the link listing, where a port is followed by its connections: "|-&gt;" for what it
    /// feeds and "|&lt;-" for what feeds it.
    /// </summary>
    /// <remarks>
    /// Every link comes back pointing the same way, from the giver to the taker, whichever of
    /// the two arrows it was printed under. A connection line before any port line is dropped,
    /// since there is nothing for it to be about.
    /// </remarks>
    IReadOnlyList<PipeWireLink> ParseLinks(string? text);

    /// <summary>
    /// True for the stereo audio ports worth offering. Video and MIDI ports live in the same
    /// listing, and neither is something a recorder can use.
    /// </summary>
    bool IsStereoAudio(string? port);

    /// <summary>"FL" or "FR", for pairing a source's ports with the capture's.</summary>
    /// <remarks>
    /// Anything that is not the right channel is the left one, which is only ever asked of a
    /// port that has already passed <see cref="IsStereoAudio"/>.
    /// </remarks>
    string Channel(string port);

    /// <summary>
    /// Everything with stereo audio to give, one entry per node. A node is judged by its name:
    /// PipeWire's own devices are prefixed, and anything else is a program.
    /// </summary>
    /// <remarks>
    /// One entry per node although a node has a port per channel, so the picker offers a thing
    /// rather than a pair of wires. The result is devices first and then what is playing, since
    /// the list is read top down when picking a source, and alphabetical within each of those.
    /// </remarks>
    /// <param name="outputs">Every port with audio to give, as the tool listed them.</param>
    /// <param name="names">
    /// Friendly names by node, where the graph has them. A node with none keeps its own name,
    /// which for a program is already what it is called.
    /// </param>
    /// <param name="exclude">
    /// A fragment of a node name to leave out, which is how the application keeps itself off
    /// its own list. Nobody wants to record this program into this program.
    /// </param>
    IReadOnlyList<AudioRoute> RoutesFrom(
        IEnumerable<PipeWirePort> outputs,
        IReadOnlyDictionary<string, string>? names = null,
        string? exclude = null);
}
