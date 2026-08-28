using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JingleBox2.Audio;

namespace JingleBox2.Audio.Interfaces;


/// <summary>
/// Where a take comes from: what can be listened to, what is being listened to, and what was
/// heard.
/// </summary>
/// <remarks>
/// One input is open at a time and two things want it, a take and the meter, so opening and
/// closing it belongs here rather than to either of them. Starting a take while the meter is
/// already listening keeps the same capture rather than closing the device and opening it again
/// under somebody's hand, and stopping either one leaves it open while the other still wants it.
///
/// What is recorded is not always something plugged in: an output's own playback can be captured
/// instead, which is a different capture path entirely and is chosen by
/// <see cref="LoopbackDevice"/>. Everything above this meets sixteen bit interleaved audio
/// either way.
/// </remarks>
public interface IRecordingService
{
    /// <summary>What can be recorded from, by name.</summary>
    /// <remarks>
    /// By name rather than by number, because a device's number moves when hardware appears or
    /// disappears and a name written into a profile has to survive that.
    /// </remarks>
    IReadOnlyList<string> GetInputDevices();

    /// <summary>Which of those to record from, or null for the system's default.</summary>
    /// <remarks>Takes effect at the next open, which <see cref="ReopenInput"/> can force.</remarks>
    string? SelectedDevice { get; set; }

    /// <summary>Starts keeping what the input hears.</summary>
    /// <remarks>
    /// Opens the input if nothing has, and keeps the meter's capture where there is one. What was
    /// kept from an earlier take is dropped, so a take begins empty.
    /// </remarks>
    void StartRecording();

    /// <summary>Stops keeping it, and closes the input unless the meter still wants it.</summary>
    void StopRecording();

    /// <summary>Whether a take is being kept.</summary>
    bool IsRecording { get; }

    /// <summary>
    /// Opens the input to watch its level without keeping any of it, so a gain can be set
    /// before the take rather than during it.
    /// </summary>
    void StartMonitoring();

    /// <summary>Stops watching, and closes the input unless a take still wants it.</summary>
    void StopMonitoring();

    /// <summary>Whether the level is being watched.</summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Set when StartRecording could not use the selected device and fell back to the
    /// system default. Null when the selected device was used as-is.
    /// </summary>
    string? LastStartWarning { get; }

    /// <summary>Gain applied to incoming audio, in dB. 0 is unity.</summary>
    double GainDb { get; set; }

    /// <summary>How many channels the input is captured with, so a meter knows what to show.</summary>
    int Channels { get; }

    /// <summary>True while clipping was seen in the last moment or so. Decays on its own.</summary>
    bool IsClipping { get; }

    /// <summary>True if anything clipped at any point during the current or last take.</summary>
    bool ClippedDuringTake { get; }

    /// <summary>The last moment of audio, for a meter to read.</summary>
    /// <remarks>
    /// Empty once nothing has arrived for a moment, because a source that stops sending stops the
    /// callbacks with it and a meter reading the last block it was given would sit lit at whatever
    /// was playing when the sound stopped. Always a whole number of frames, so the samples in it
    /// stay in step with their channels.
    /// </remarks>
    /// <param name="maxBytes">At most this much, taken from the end.</param>
    byte[] GetRecentRecordingData(int maxBytes);

    /// <summary>Writes the take to the recordings folder, at whatever rate it came in at.</summary>
    /// <param name="fileName">The take's name, without the extension.</param>
    /// <returns>Where it was written.</returns>
    /// <exception cref="InvalidOperationException">There is nothing to save, or it could not be written.</exception>
    Task<string> SaveRecordingAsync(string fileName);

    /// <summary>
    /// The output to record the playback of, or null to record from the selected input device.
    /// Setting it while the input is open reopens it on the other path.
    /// </summary>
    int? LoopbackDevice { get; set; }

    /// <summary>The outputs whose playback can be captured. Empty where the system cannot.</summary>
    IReadOnlyList<LoopbackDevice> GetLoopbackDevices();

    /// <summary>
    /// Closes and reopens the input if anything is listening, for a change that only takes
    /// effect on a fresh capture. Does nothing when nothing is open.
    /// </summary>
    void ReopenInput();
}
