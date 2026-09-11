using JingleBox2.Audio.Records;
using System;
using System.IO;

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
    /// Read into a fixed number of columns, so a picture can be drawn at any width without reading
    /// the file again. A recording with fewer frames than that comes back with a peak a frame
    /// instead, since more columns than there are frames is detail that is not in the file.
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
    /// Silences part of a recording, leaving everything else and its length alone.
    /// </summary>
    /// <remarks>
    /// The other half of what a region is for. Trimming keeps the region and throws the rest
    /// away; this keeps the rest and empties the region, which is what somebody wants when a
    /// cough or a stray word landed in the middle of a take that is otherwise good.
    ///
    /// The length does not change, deliberately: taking the frames out instead would move
    /// everything after them, and a pad, an instrument or a slice pointing into this file by
    /// position would quietly be pointing at something else. Silence is the edit that leaves
    /// every other thing that knows this take still right about it.
    /// </remarks>
    /// <param name="filePath">The take.</param>
    /// <param name="startFrame">Where the silence starts.</param>
    /// <param name="endFrame">Where it ends, exclusive.</param>
    void SilenceFile(string filePath, long startFrame, long endFrame);

    /// <summary>
    /// Turns part of a recording back to front, leaving everything else and its length alone.
    /// </summary>
    /// <remarks>
    /// The region the handles stand over rather than the whole take, since the whole take is the
    /// region when both handles are on the ends: one rule covers reversing a word somebody said
    /// backwards and reversing a cymbal to swell into a beat.
    /// </remarks>
    /// <param name="filePath">The take.</param>
    /// <param name="startFrame">Where the reversal starts.</param>
    /// <param name="endFrame">Where it ends, exclusive.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="InvalidOperationException">The region holds no frames.</exception>
    void ReverseFile(string filePath, long startFrame, long endFrame);

    /// <summary>
    /// Ramps part of a recording up from silence or down to it, leaving its length alone.
    /// </summary>
    /// <remarks>
    /// What a take off a microphone almost always wants before it is used for anything: a head
    /// that starts on a click and a tail that stops on one are the two faults every raw
    /// recording has, and both are a fade a fraction of a second long.
    /// </remarks>
    /// <param name="filePath">The take.</param>
    /// <param name="startFrame">Where the fade starts.</param>
    /// <param name="endFrame">Where it ends, exclusive.</param>
    /// <param name="rising">True to come up from silence, false to go down to it.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="InvalidOperationException">The region holds no frames.</exception>
    void FadeFile(string filePath, long startFrame, long endFrame, bool rising);


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
