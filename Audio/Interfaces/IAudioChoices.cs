using System.Collections.Generic;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Which buffer, top-up and cushion sizes are offered, and which of them is recommended, with and
/// without real-time audio.
/// </summary>
/// <remarks>
/// Real-time audio is the system's to give, and where it is given the mixing can be held to much
/// shorter deadlines without breaking up. So it decides two things here: smaller sizes are offered
/// on top of what everybody gets, for whoever wants to try them, and the recommended cushion moves
/// down, since the cushion is the mixing's own lead. The recommended buffer does not move, because
/// the output is filled by threads that are not real time either way.
///
/// The recommendation is also what runs when nothing has been set, so a fresh installation runs
/// at the right sizes for its machine without anybody opening the settings, and the settings page
/// marks the same values as recommended: one rule, read by both.
/// </remarks>
public interface IAudioChoices
{
    /// <summary>The output buffer sizes offered, in frames, smallest first.</summary>
    /// <param name="realtime">Whether the system allows real-time audio.</param>
    IReadOnlyList<int> BufferFrames(bool realtime);

    /// <summary>How often the output may be topped up, in milliseconds, quickest first.</summary>
    /// <param name="realtime">Whether the system allows real-time audio.</param>
    IReadOnlyList<int> UpdatePeriods(bool realtime);

    /// <summary>How far ahead the mixer may work, in milliseconds, nought first.</summary>
    /// <param name="realtime">Whether the system allows real-time audio.</param>
    IReadOnlyList<int> Cushions(bool realtime);

    /// <summary>The buffer, the top-up and the number of filling threads recommended.</summary>
    /// <remarks>
    /// The buffer is the smallest offered that holds two of the sound server's cycles at the rate
    /// the application runs at, since the server takes a whole cycle at once and one has to be ready
    /// while the other plays. Where the server could not be asked it is 2048 frames, which is what
    /// this application has been played at.
    /// </remarks>
    /// <param name="realtime">Whether the system allows real-time audio.</param>
    /// <param name="clock">The sound server's cycle, or nothing where it could not be asked.</param>
    /// <param name="rate">The rate the application runs at, in Hz.</param>
    AudioSizes Recommended(bool realtime, ServerClock? clock, int rate);

    /// <summary>The rates offered for the application to run at, in Hz, lowest first.</summary>
    IReadOnlyList<int> Rates { get; }

    /// <summary>
    /// The rate recommended: the sound server's own where it is offered, so nothing is converted on
    /// the way out, and 44100 where it is not or the server could not be asked.
    /// </summary>
    /// <param name="clock">The sound server's cycle, or nothing where it could not be asked.</param>
    int RecommendedRate(ServerClock? clock);

    /// <summary>The cushion recommended, in milliseconds.</summary>
    /// <param name="realtime">Whether the system allows real-time audio.</param>
    int RecommendedCushion(bool realtime);
}
