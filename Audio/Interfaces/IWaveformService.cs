using JingleBox2.Models;
using System;
using System.IO;
using JingleBox2.Audio;

namespace JingleBox2.Audio.Interfaces;


/// <summary>
/// What a recording looks like, and the two edits that change what it holds.
/// </summary>
/// <remarks>
/// Reading and editing sit behind one door because they answer to the same file and the same
/// reader. Both edits rewrite the recording where it lies, which is what makes this a seam worth
/// having: everything above it can be handed something that reads a file and writes nothing.
/// </remarks>
public interface IWaveformService
{
    /// <summary>The picture of a recording: its peaks, its rate, its channels and its length.</summary>
    /// <remarks>
    /// A fixed number of columns whatever the recording's length, so a picture can be drawn at any
    /// width without reading the file again.
    /// </remarks>
    /// <param name="filePath">The recording.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    WaveformData AnalyzeFile(string filePath);

    /// <summary>Duration of a recording, read from its headers alone.</summary>
    /// <param name="filePath">The recording.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    TimeSpan GetDuration(string filePath);

    /// <summary>How many sample frames a recording holds, from its headers alone.</summary>
    /// <param name="filePath">The recording, which may be one that is not there.</param>
    /// <returns>The frame count, and nought for a file that does not exist.</returns>
    long GetFrameCount(string filePath);

    /// <summary>
    /// Rewrites the file to contain only the frames in [startFrame, endFrame). Destructive:
    /// the original audio outside the region is gone once this returns.
    /// </summary>
    /// <param name="filePath">The recording.</param>
    /// <param name="startFrame">The first frame to keep, clamped into the file.</param>
    /// <param name="endFrame">One past the last frame to keep, clamped to at least the start.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="InvalidOperationException">The region holds no frames.</exception>
    void TrimFile(string filePath, long startFrame, long endFrame);

    /// <summary>
    /// Lifts the whole file so its loudest moment sits on the target, in dBFS. Destructive,
    /// like the trim. Returns how far it moved in decibels, which is zero when the recording
    /// was already there or has nothing in it to lift.
    /// </summary>
    /// <param name="filePath">The recording.</param>
    /// <param name="targetDecibels">Where the loudest moment should end up, in dBFS.</param>
    /// <returns>How far the recording moved, in decibels.</returns>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    double NormalizeFile(string filePath, double targetDecibels);
}
