namespace JingleBox2.Diagnostics.Records;

/// <summary>
/// Bytes going two ways: in and out of the network, or read from and written to the discs.
/// </summary>
/// <param name="InRate">Bytes a second coming in, or being read, since the last reading.</param>
/// <param name="OutRate">Bytes a second going out, or being written.</param>
/// <param name="InTotal">All the bytes that have come in, or been read, since the computer started.</param>
/// <param name="OutTotal">And all that have gone out, or been written.</param>
public readonly record struct TrafficFigures(double InRate, double OutRate, long InTotal, long OutTotal);
