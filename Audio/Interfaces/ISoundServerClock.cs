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
    /// <remarks>Asked once per run and kept, since a server does not change its cycle under a running program.</remarks>
    ServerClock? Read();

    /// <summary>
    /// The cycle out of what PipeWire prints about its settings, the forced values winning over
    /// the ordinary ones where somebody has set them; nothing where it is not all there.
    /// </summary>
    /// <param name="settings">What <c>pw-metadata -n settings 0</c> printed.</param>
    ServerClock? FromPipeWire(string settings);
}
