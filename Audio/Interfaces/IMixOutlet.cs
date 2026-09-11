namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Where the summed mix leaves this application.
/// </summary>
/// <remarks>
/// **There are three ways out and they are one question.** A driver holds the card and pulls the
/// mix out of us; a sound server holds the machine's graph and pulls it the same way; and where
/// neither does, the library plays the mix itself. Which of the three is a fact about the machine
/// and about what was picked in SETTINGS, and nothing above it is a fact about the operating
/// system: a driver is Windows in practice and a server is Linux in practice, and neither is
/// written down anywhere as a platform.
///
/// It was two flags and two branches threaded through three methods, which is the same fact said
/// twice and then asked twice. That is where it went wrong: the test for the server was made of
/// a name and a default that the one output nobody has picked yet cannot supply, so a machine
/// whose whole graph was waiting to pull the mix played it through the library instead, and there
/// was no one place to look.
///
/// <see cref="Pulls"/> is the whole of what the rest of the engine needs to know, since it
/// decides two things at once: whether the bus is a decoding stream, and whether the library is
/// opened on the device that plays nothing.
/// </remarks>
public interface IMixOutlet
{
    /// <summary>Whether it takes the mix out itself rather than the library playing it.</summary>
    bool Pulls { get; }

    /// <summary>What it is called in a line about the output, or nothing where it is the library.</summary>
    /// <remarks>
    /// The words a sentence is built round rather than the sentence, since the same name is
    /// wanted in two of them: what is pulling, and what would not take the bus.
    /// </remarks>
    string Word { get; }

    /// <summary>Takes the mix.</summary>
    /// <param name="stream">The output bus.</param>
    /// <param name="rate">What it is summed at.</param>
    /// <returns>False where it would not, which is a machine that will hear nothing.</returns>
    bool Open(int stream, int rate);

    /// <summary>Lets the mix go again. Safe on one that was never opened.</summary>
    void Close();

    /// <summary>What it has to say about having refused, or nothing where it has nothing.</summary>
    /// <remarks>
    /// Each of the three knows a different reason and none of the others can read it: a library
    /// keeps its own last error, and a driver and a server each know whether they are there at
    /// all. Asked of the outlet, a line about a mix that will not leave says why without the
    /// caller having to know which of the three it was talking to.
    /// </remarks>
    string Why { get; }
}
