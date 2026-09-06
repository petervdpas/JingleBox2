namespace JingleBox2.UI.Enums;

/// <summary>How many channels a connection point carries.</summary>
/// <remarks>
/// The numbers are the count itself rather than an arbitrary order, so how many dots to draw is
/// the enum cast to an integer rather than a second table saying the same thing. A microphone
/// is one, a browser is two, and a headset in its telephone profile is one where the same
/// device is two in its music profile.
/// </remarks>
public enum PatchChannels
{
    /// <summary>One channel, drawn as a single dot.</summary>
    Mono = 1,

    /// <summary>Two channels, drawn as a pair.</summary>
    Stereo = 2
}
