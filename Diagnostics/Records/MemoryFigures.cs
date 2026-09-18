namespace JingleBox2.Diagnostics.Records;

/// <summary>
/// What the computer's memory stands at, in bytes.
/// </summary>
/// <param name="Total">All the memory there is.</param>
/// <param name="Available">What could be handed out now without anything being pushed to swap.</param>
/// <param name="SwapTotal">All the swap there is, and nought where there is none.</param>
/// <param name="SwapFree">What of the swap is not in use.</param>
/// <param name="Cache">
/// What is holding copies of files, which is handed back the moment anything asks for it, and
/// nought where the system does not say.
/// </param>
public readonly record struct MemoryFigures(long Total, long Available, long SwapTotal, long SwapFree, long Cache = 0);
