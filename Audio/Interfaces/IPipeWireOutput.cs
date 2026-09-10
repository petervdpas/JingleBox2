namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// This application as a node on the sound server's own graph, pulling the mix out of it.
/// </summary>
/// <remarks>
/// **What everything went through before this was the server's compatibility layer, and that is
/// not the same thing as being on the graph.** Opened that way the application appears as an ALSA
/// stream wearing whatever shape the card's profile has, four ports out on a surround interface
/// with two of them silent, and there is nothing joining it to the stream the recorder captures
/// with: two unrelated clients that happen to share a name. What somebody looking at their own
/// patchbay sees is nothing like the picture this application draws of itself.
///
/// A node is the other thing. It is this application, named, the width it actually is, and in one
/// place, so a source going into it and the mix coming out of it read as one chain.
///
/// **The shape is the one the drivers already use, and deliberately so.** A driver owns the card
/// and pulls, so the library is opened on its own silent device and the mix is made a decoding
/// stream for the driver to take. The server is the same arrangement with a different puller:
/// see <see cref="IAsioDevices.Open"/>, which this is written to match line for line, because a
/// second way of delivering the mix is a second thing to keep in step.
///
/// **False everywhere but Linux, and nothing of the library is touched there.** The one behind
/// this is a managed wrapper over the server's own, and a machine without the server has neither.
/// Everything above here therefore has to work when the answer is no, which is every Windows
/// machine and every Linux one without it running: that is what <see cref="Present"/> says, and
/// it is asked before anything else.
/// </remarks>
public interface IPipeWireOutput
{
    /// <summary>True where the server is really here and could be asked.</summary>
    /// <remarks>
    /// Asked of the machine rather than guessed from the platform, since a Linux machine with no
    /// server behaves exactly like a Windows one. Answered once and remembered: it cannot change
    /// while the program runs, and asking costs loading a library that may not be there.
    /// </remarks>
    bool Present { get; }

    /// <summary>Why it is not here, in words for a person, or nothing when it is.</summary>
    string Missing { get; }

    /// <summary>Whether the mix is going out through it at this moment.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Puts this application on the graph and starts pulling the mix through it.
    /// </summary>
    /// <remarks>
    /// The stream has to be a decoding one, for the reason a driver's does: this pulls from it,
    /// so anything the library was playing on its own would be the same audio leaving by two
    /// routes.
    ///
    /// Stereo and nothing else, which is what the whole application is. Where the node is wired
    /// to is the machine's business rather than this one's: it lands wherever the session manager
    /// puts a new stream, which is what somebody has already chosen in their own settings.
    /// </remarks>
    /// <param name="stream">The decoding mix, by its library handle.</param>
    /// <param name="rate">Frames a second.</param>
    /// <returns>False where it could not be opened, which leaves nothing running.</returns>
    bool Open(int stream, int rate);

    /// <summary>Takes it off the graph, and does nothing where it was not on it.</summary>
    void Close();
}
