namespace JingleBox2.Audio.Records;

/// <summary>
/// The three numbers that decide whether the sound comes out whole.
/// </summary>
/// <remarks>
/// They are one record because they are one decision. A buffer is only as good as how often it is
/// topped up, and how often it is topped up is only as good as how many threads are doing the
/// topping, so choosing one of them without the other two is choosing nothing.
/// </remarks>
/// <param name="BufferFrames">How much audio the sound card holds ahead of what you hear.</param>
/// <param name="UpdatePeriodMs">How often the sound library tops that buffer up.</param>
/// <param name="UpdateThreads">
/// How many threads do the topping up. Nought leaves the sound library on its own default, which
/// is one.
/// </param>
public readonly record struct AudioSizes(int BufferFrames, int UpdatePeriodMs, int UpdateThreads);
