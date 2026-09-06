using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JingleBox2.Audio.Records;

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

    /// <summary>Writes the take out, at whatever rate it came in at.</summary>
    /// <remarks>
    /// **Into whatever folder it is told and never into the shelf's**, which is the whole of what
    /// makes a take disposable until somebody names it: what comes off the input goes to the
    /// scratchpad, and reaching the recordings folder is a separate act with a name in it. See
    /// <see cref="ITakeScratch"/>.
    ///
    /// Both files where there is a chain on <see cref="Effect"/>, and one where there is not. The
    /// take under the first name is the one that went through the chain, because that is the
    /// sound somebody set a chain up to record; the capture as it arrived is written beside it
    /// under the other name, since an effect cannot be taken off a take afterwards.
    ///
    /// The chain is run here rather than while the audio was arriving, and
    /// <see cref="ITakeEffects"/> is where the reason for that is written down.
    /// </remarks>
    /// <param name="folder">Where to write, made if it is not there.</param>
    /// <param name="fileName">The take's name there, without the extension.</param>
    /// <param name="cleanName">
    /// What to call the untouched capture, without the extension. Ignored where there is no
    /// chain, since then it would be the same audio written twice.
    /// </param>
    /// <returns>Where each of them was written.</returns>
    /// <exception cref="InvalidOperationException">There is nothing to save, or it could not be written.</exception>
    Task<SavedTake> WriteTakeAsync(string folder, string fileName, string cleanName);

    /// <summary>
    /// What every take is run through on its way to the shelf, or null to keep them as captured.
    /// </summary>
    /// <remarks>
    /// Held rather than handed in per take, because it is a chain somebody built on the page and
    /// left there: it belongs to the recorder the way the input gain does.
    /// </remarks>
    Plugins.Interfaces.IAudioInsert? Effect { get; set; }

    /// <summary>The rate the capture is running at, which a chain has to be built for.</summary>
    /// <remarks>
    /// The device's own rate on the ordinary path and the output's where a loopback is being
    /// captured, so it is only true once the input has been opened. Before that it is what the
    /// next take will be opened at.
    /// </remarks>
    int SampleRate { get; }

    /// <summary>
    /// The output to record the playback of, or null to record from the selected input device.
    /// Setting it while the input is open reopens it on the other path.
    /// </summary>
    int? LoopbackDevice { get; set; }

    /// <summary>The outputs whose playback can be captured. Empty where the system cannot.</summary>
    IReadOnlyList<LoopbackDevice> GetLoopbackDevices();

    /// <summary>Every program on this machine that is playing something. Empty where it cannot be asked.</summary>
    /// <remarks>
    /// A program is in the list while it holds an audio session and not otherwise, which is the
    /// same rule the PipeWire side keeps: a browser with nothing playing is not there, and it
    /// turns up the moment it makes a sound.
    /// </remarks>
    IReadOnlyList<AudioProgram> GetPrograms();

    /// <summary>
    /// The program to record, or null to record a device or an output instead.
    /// </summary>
    /// <remarks>
    /// **The third of the three ways to listen, and the one that makes Windows behave the way
    /// Linux does**: a PipeWire machine points the input at one program through the graph, and
    /// this is what stands in for that where there is no graph. It wins over
    /// <see cref="LoopbackDevice"/>, since a program is the narrower answer and setting one is a
    /// deliberate act.
    ///
    /// Setting it while the input is open reopens it on the other path.
    /// </remarks>
    int? LoopbackProgram { get; set; }

    /// <summary>
    /// Says where what is coming in is heard, which is the desk's own input channel.
    /// </summary>
    /// <remarks>
    /// Handed in rather than made here, because the stream it holds is a source on a bus and the
    /// busses belong to the engine. Told once, at startup, since there is one of each.
    /// </remarks>
    /// <param name="monitor">The path onto the input's bus.</param>
    void HearThrough(IMonitorFeed monitor);

    /// <summary>
    /// Whether what is coming in is played out of the desk while it comes in.
    /// </summary>
    /// <remarks>
    /// **Off unless somebody says so, and never remembered**, since the ordinary source is what
    /// an output is playing and hearing that through the output is a loop. What is heard is what
    /// a take would hold, chain and input gain included, which is the point of listening at all.
    ///
    /// Setting it while nothing is captured is not refused: the path is opened when the capture
    /// is, at whatever rate it turns out to be running.
    /// </remarks>
    bool Hearing { get; set; }

    /// <summary>
    /// Closes and reopens the input if anything is listening, for a change that only takes
    /// effect on a fresh capture. Does nothing when nothing is open.
    /// </summary>
    void ReopenInput();
}
