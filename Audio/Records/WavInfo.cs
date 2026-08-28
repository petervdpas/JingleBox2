namespace JingleBox2.Audio.Records;

/// <summary>What a WAV file's headers say it is.</summary>
/// <param name="SampleRate">Frames a second.</param>
/// <param name="Channels">How many samples one frame holds.</param>
/// <param name="FrameCount">How many frames the file holds, which is not its sample count.</param>
public readonly record struct WavInfo(int SampleRate, int Channels, long FrameCount);
