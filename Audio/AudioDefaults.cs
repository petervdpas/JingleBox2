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
    /// <summary>
    /// What Linux is given: 2048 frames, which is 46 ms at 44100.
    /// </summary>
    /// <remarks>
    /// The nearest stop on the slider to the sixty milliseconds this application ran as a
    /// constant for weeks and which is the only figure anybody has actually listened to.
    /// </remarks>
    private static readonly AudioSizes Linux = new(2048, 10, 1);

    /// <summary>What Windows is given, until it has been measured there.</summary>
    private static readonly AudioSizes Windows = new(2048, 10, 1);

    /// <inheritdoc/>
    public AudioSizes For(bool windows) => windows ? Windows : Linux;

    /// <summary>Whether the system allows real-time audio, which moves what is recommended.</summary>
    private readonly IRealtimeThread _realtime;

    /// <summary>What is recommended with and without real time.</summary>
    private readonly IAudioChoices _choices;

    /// <summary>The sound server's cycle, which the recommended buffer follows.</summary>
    private readonly ISoundServerClock _clock;

    /// <summary>Builds one that answers for this machine.</summary>
    /// <param name="realtime">What the system allows, the real answer where nothing is said.</param>
    /// <param name="choices">What is recommended, the shipped rule where nothing is said.</param>
    /// <param name="clock">The sound server's cycle, asked of the real server where nothing is said.</param>
    public AudioDefaults(IRealtimeThread? realtime = null, IAudioChoices? choices = null, ISoundServerClock? clock = null)
    {
        _realtime = realtime ?? new RealtimeThread();
        _choices = choices ?? new AudioChoices();
        _clock = clock ?? new SoundServerClock();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// What is recommended for this machine's sound server, worked out at the rate recommended for
    /// it, so a machine runs at a buffer that holds two of its server's cycles without anybody
    /// setting it; where the server cannot be asked that is <see cref="For"/>'s numbers.
    /// </remarks>
    public AudioSizes Here
    {
        get
        {
            var cycle = _clock.Read();

            return _choices.Recommended(_realtime.Allowed, cycle, _choices.RecommendedRate(cycle));
        }
    }

    /// <inheritdoc/>
    public AudioSizes Chosen(AudioSizes stored)
    {
        var fallback = Here;

        return new AudioSizes(
            stored.BufferFrames > 0 ? stored.BufferFrames : fallback.BufferFrames,
            stored.UpdatePeriodMs > 0 ? stored.UpdatePeriodMs : fallback.UpdatePeriodMs,
            stored.UpdateThreads > 0 ? stored.UpdateThreads : fallback.UpdateThreads);
    }

    /// <inheritdoc/>
    public int Cushion(int stored) => stored < 0 ? _choices.RecommendedCushion(_realtime.Allowed) : stored;
}
