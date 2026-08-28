using JingleBox2.Audio.Enums;

namespace JingleBox2.Audio;


/// <summary>A pad started, stopped, or failed to do either.</summary>
/// <param name="PadIndex">Which pad it was about.</param>
/// <param name="State">What it is doing now.</param>
/// <param name="Message">Why, for an error, and null otherwise.</param>
public sealed record PadPlaybackChanged(
    int PadIndex,
    PadPlaybackState State,
    string? Message = null
);
