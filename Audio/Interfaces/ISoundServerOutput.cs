namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Which of the outputs a machine lists is the sound server's own.
/// </summary>
/// <remarks>
/// **The list on Linux reads as a set of choices and they do not behave alike.** The library
/// enumerates ALSA, which answers with the card, a handful of conversion plugins, a default that
/// is the server reached the long way round, and the server itself. Every one of them is a real
/// thing somebody might mean, and one of them is the one this application can be a proper node
/// on: picked, the mix goes out through <see cref="IPipeWireOutput"/> and appears on the graph as
/// this application, two ports wide and named. Picked otherwise, everything goes the way it
/// always did, through the library's own device.
///
/// So this decides nothing about what is offered. It answers one question about one name, and
/// the answer chooses a path rather than shortening a list: a machine is entitled to its card,
/// its plugins and its default, and somebody who picks one of those has said what they meant.
///
/// Named rather than numbered, since the entry is an ALSA plugin whose place in the list moves
/// with whatever else is installed, while its description comes from the plugin itself and is the
/// same on every machine that has it.
/// </remarks>
public interface ISoundServerOutput
{
    /// <summary>
    /// Whether that output is the sound server, either by name or by being the system's default.
    /// </summary>
    /// <remarks>
    /// **The default counts, and the operating system is what says so.** Where the server is
    /// running it owns what everything else on the machine calls the default output: the entry
    /// named for the server and the entry named default are two ways of reaching one thing. So
    /// picking either takes the same path, and nobody has to know which of two plausible rows
    /// means what.
    ///
    /// Asked of the library's own answer rather than worked out here. Whether an output is the
    /// default is a fact the system keeps and reports, and a second opinion about it would be
    /// this application guessing at somebody's settings.
    ///
    /// Whether the server is running at all is the caller's question, since it is asked of the
    /// server rather than of a name: see <see cref="IPipeWireOutput.Present"/>. With it absent
    /// the default is a default and nothing here applies.
    /// </remarks>
    /// <param name="name">What the library calls the output.</param>
    /// <param name="standard">Whether the system calls this one its default.</param>
    bool Is(string? name, bool standard);
}
