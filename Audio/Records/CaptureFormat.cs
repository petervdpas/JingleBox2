namespace JingleBox2.Audio.Records;

/// <summary>What a block of captured audio is made of, before anything here reads it.</summary>
/// <remarks>
/// **Everything above the capture meets sixteen bit interleaved audio**, so this exists only for
/// the moment between a capture handing a block over and that being true. A capture path is
/// entitled to its own shape: WASAPI hands out whatever the device mixes at, and a per-process
/// capture cannot even be asked what that is, so what arrives is decided by what was asked for
/// and may be floats.
/// </remarks>
/// <param name="SampleRate">Frames a second.</param>
/// <param name="Channels">How many channels are interleaved in it.</param>
/// <param name="Bits">How wide one sample is.</param>
/// <param name="Floats">Whether those bits are a floating point number rather than an integer.</param>
public readonly record struct CaptureFormat(int SampleRate, int Channels, int Bits, bool Floats);
