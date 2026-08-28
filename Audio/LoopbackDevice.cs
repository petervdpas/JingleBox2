using System;

namespace JingleBox2.Audio;


/// <summary>One of the outputs whose playback can be captured.</summary>
/// <param name="Index">Which output it is, as WASAPI numbers them.</param>
/// <param name="Name">What it is called, as the system reports it.</param>
public readonly record struct LoopbackDevice(int Index, string Name);
