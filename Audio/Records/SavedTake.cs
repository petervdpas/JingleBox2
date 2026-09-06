namespace JingleBox2.Audio.Records;

/// <summary>
/// Where a take was written, and where the untouched capture went where one was kept.
/// </summary>
/// <remarks>
/// Two files rather than one because RECORD has a chain: what somebody sets a chain up for is
/// the sound they meant to record, so <see cref="Path"/> is the take under the name they typed,
/// and <see cref="Clean"/> is the capture exactly as it arrived, kept because an effect cannot
/// be taken off afterwards. There is no clean twin where the chain is empty, since then the two
/// files would be the same audio under two names.
/// </remarks>
/// <param name="Path">The take, through whatever was on the chain.</param>
/// <param name="Clean">The untouched capture, or null where there was nothing to keep it from.</param>
public readonly record struct SavedTake(string Path, string? Clean);
