using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What the audio is sized at when nobody has said otherwise, which is not the same answer on
/// every system.
/// </summary>
/// <remarks>
/// **The defaults are per platform, and that is not a detail.** On Linux the sound library talks
/// to PulseAudio or PipeWire, which is already buffering underneath us, so what is asked for here
/// sits on top of somebody else's cushion and a small number is smaller than it looks. On Windows
/// it is DirectSound, or WASAPI if this application ever opens one itself, and the same number
/// behaves differently again. One default for both is how a value that is comfortable on one
/// machine arrives broken on another.
///
/// **Asked with the platform rather than reading it.** A rule that looks up the operating system
/// inside itself cannot be put a question to: a machine running Linux could never be asked what
/// Windows would have decided, and the half of this application that decides how audio is sized is
/// exactly the half where being quietly wrong is inaudible until somebody else runs it. Handed the
/// answer instead, both cases can be checked on either machine, which is what
/// <c>Tests/AudioDefaultsTests.cs</c> does.
/// </remarks>
public interface IAudioDefaults
{
    /// <summary>What to use where nothing has been chosen.</summary>
    /// <param name="windows">True for Windows, false for everything else.</param>
    AudioSizes For(bool windows);

    /// <summary>What this machine is, so a caller that only wants the answer can have it.</summary>
    AudioSizes Here { get; }

    /// <summary>
    /// A stored value, or the default for this machine where the stored value is nought.
    /// </summary>
    /// <remarks>
    /// Nought means "whatever suits this machine" rather than nought milliseconds, which is what
    /// lets a settings file written before any of this existed keep sounding exactly as it did,
    /// and what lets the same file be carried between a Linux machine and a Windows one without
    /// carrying a number that suited neither.
    /// </remarks>
    /// <param name="stored">What the settings hold, or nought for nothing chosen.</param>
    AudioSizes Chosen(AudioSizes stored);
}
