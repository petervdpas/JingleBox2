using JingleBox2.Audio.Enums;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Which of the three ways out the mix takes.
/// </summary>
/// <remarks>
/// The rule on its own, so it can be put a question to without a sound card: a machine with a
/// sound server and the system's own default picked has to answer that the server is pulling, and
/// that is a sentence rather than a thing anybody can hear.
/// </remarks>
public interface IMixOutlets
{
    /// <summary>The outlet for that output.</summary>
    /// <param name="kind">Which of the two worlds the stored number named.</param>
    /// <param name="index">Its own number inside that world.</param>
    /// <param name="named">What the library calls it, which is empty where it could not say.</param>
    /// <param name="standard">Whether the library marks it as the system's own default.</param>
    IMixOutlet For(AudioOutputKind kind, int index, string named, bool standard);
}
