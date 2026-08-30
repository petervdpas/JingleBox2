using System;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// The Linux numbers are the ones this application shipped with as constants and which were played
/// for weeks: sixty milliseconds of buffer topped up every ten. They are written here as the
/// default rather than improved on, because they are the only pair anybody has actually listened
/// to.
///
/// The Windows numbers are deliberately the same until somebody measures them there. A guess
/// dressed as a platform default is worse than an honest copy: this way the day somebody runs it
/// on Windows and hears something, there is one place to put what they heard.
/// </remarks>
public sealed class AudioDefaults : IAudioDefaults
{
    /// <summary>What Linux is given.</summary>
    private static readonly AudioSizes Linux = new(60, 10, 0);

    /// <summary>What Windows is given, until it has been measured there.</summary>
    private static readonly AudioSizes Windows = new(60, 10, 0);

    /// <inheritdoc/>
    public AudioSizes For(bool windows) => windows ? Windows : Linux;

    /// <inheritdoc/>
    public AudioSizes Here => For(OperatingSystem.IsWindows());

    /// <inheritdoc/>
    public AudioSizes Chosen(AudioSizes stored)
    {
        var fallback = Here;

        return new AudioSizes(
            stored.BufferMs > 0 ? stored.BufferMs : fallback.BufferMs,
            stored.UpdatePeriodMs > 0 ? stored.UpdatePeriodMs : fallback.UpdatePeriodMs,
            stored.UpdateThreads > 0 ? stored.UpdateThreads : fallback.UpdateThreads);
    }
}
