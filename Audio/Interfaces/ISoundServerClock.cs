using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Asks the sound server what cycle it runs at, so the buffer and the rate can follow it.
/// </summary>
/// <remarks>
/// The sound card does give its optimum, through the server in front of it: PipeWire moves audio
/// in fixed cycles of so many frames at so many Hz, and Windows in a device period. A buffer
/// shorter than two of those runs dry however fast the mixing is, and a rate other than the
/// server's is converted on the way out. So the recommendation is read off the server rather than
/// guessed, and where the server cannot be asked the answer is that it is not known, which leaves
/// the sizes this application has been played at.
/// </remarks>
public interface ISoundServerClock
{
    /// <summary>The server's cycle on this machine, or nothing where it cannot be asked.</summary>
    /// <remarks>
    /// Asked the first time and kept, and the same answer for every reader in the process, since
    /// the page that recommends a buffer and the tracker that opens one have to agree about the
    /// server they are both talking to. Reading is free after the first time, which is what lets
    /// a binding read it.
    /// </remarks>
    ServerClock? Read();

    /// <summary>
    /// Asks the server again and keeps the new answer for every reader after it.
    /// </summary>
    /// <remarks>
    /// A server's cycle is somebody's setting and can be changed under a running program, by
    /// forcing PipeWire's quantum for instance, and what is recommended here would otherwise go on
    /// following the cycle the server had when the program started. This is a round trip to the
    /// server's own tool, up to a second, so it is for a deliberate press and never for a binding.
    /// </remarks>
    ServerClock? ReadAgain();

    /// <summary>
    /// The cycle out of what PipeWire prints about its settings, the forced values winning over
    /// the ordinary ones where somebody has set them; nothing where it is not all there.
    /// </summary>
    /// <param name="settings">What <c>pw-metadata -n settings 0</c> printed.</param>
    ServerClock? FromPipeWire(string settings);
}
