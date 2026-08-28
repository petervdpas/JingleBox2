namespace JingleBox2.Audio.Enums;

/// <summary>What a pad is doing, as far as anything outside the engine is concerned.</summary>
public enum PadPlaybackState
{
    /// <summary>Silent, whether it has never played or has reached its end.</summary>
    Stopped,

    /// <summary>Sounding, which for a stream includes waiting on the connection.</summary>
    Playing,

    /// <summary>It could not be played, and the message says why in words a person can read.</summary>
    Error
}
