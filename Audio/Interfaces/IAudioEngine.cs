using System;
using System.Collections.Generic;
using JingleBox2.Audio.Records;
using JingleBox2.Config.Enums;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The pads' sound: what plays them, what they play through, and what they are doing now.
/// </summary>
/// <remarks>
/// One engine for the whole application, and the tracker shares its device rather than opening
/// a second one. Two things opening the same card is the shortest road to an application that
/// works alone and is silent the moment anything else is running.
///
/// Everything here is indexed by pad, and every one of them holds against a number outside the
/// range rather than throwing: pads are made and unmade while the rest of the program is running
/// (see <see cref="Resize"/>), and a caller holding a stale index is an ordinary state rather
/// than a fault.
/// </remarks>
public interface IAudioEngine : IDisposable
{
    /// <summary>How many pads there are, which is a setting and can change while running.</summary>
    int PadCount { get; }

    /// <summary>
    /// The loudest thing the pads are putting out, 0 to 1, or nought when none of them is.
    /// </summary>
    /// <remarks>
    /// Half of what the status bar's output meter shows. The other half is the tracker's own
    /// stream, which is a different channel and belongs to a different object; whoever wants the
    /// main output takes the louder of the two.
    /// </remarks>
    float GetOutputLevel();

    /// <summary>Every output the machine offers, in the order the system lists them.</summary>
    IEnumerable<AudioOutput> GetOutputDevices();

    /// <summary>
    /// Sends everything to that output from now on.
    /// </summary>
    /// <remarks>
    /// This takes the whole sound down and brings it up again on the new card, which costs every
    /// stream that was open: whatever was playing stops, and anything holding a handle is holding
    /// a stale one. The tracker's stream goes with it, which is why it checks its stream is
    /// really still running rather than trusting the handle it was given.
    /// </remarks>
    void SetOutputDevice(int deviceId);

    /// <summary>
    /// Which way out the chosen output goes: the system's own path, or an ASIO driver.
    /// </summary>
    /// <remarks>
    /// Asked before a stream is made rather than after, because the two want different streams. A
    /// stream on the system's path plays itself; one going to an ASIO driver has to be a decoding
    /// stream that the driver pulls from, and a stream that does both is the same audio leaving
    /// by two routes at once.
    /// </remarks>
    Enums.AudioOutputKind OutputKind => Enums.AudioOutputKind.System;

    /// <summary>
    /// Why some outputs are not in the list, or nothing when they all are.
    /// </summary>
    /// <remarks>
    /// A picker that is simply missing the drivers somebody expects looks broken, and the reason
    /// is never guessable from it: ASIO needs a library that is not part of this program and has
    /// to be put beside it. Said here rather than worked out on the page, since the page cannot
    /// know whether the file is there.
    /// </remarks>
    string OutputsMissing => "";

    /// <summary>
    /// Puts a decoding stream on the chosen driver, for an output that has to be fed.
    /// </summary>
    /// <remarks>
    /// False where the chosen output is the system's, which plays its own streams and needs
    /// nobody to pull them, and false where the driver refused. A caller that is told no has a
    /// stream nothing is pulling, and its answer is to play it the ordinary way.
    /// </remarks>
    /// <param name="stream">The decoding stream to pull from.</param>
    /// <param name="rate">The rate the mix is made at.</param>
    bool Feed(int stream, int rate) => false;

    /// <summary>
    /// How many frames a block is on the output that is really running, or nought where the
    /// output does not decide that for itself.
    /// </summary>
    /// <remarks>
    /// An ASIO driver does: its own panel sets the block, this program does not choose one, and
    /// what somebody sees on a settings page has to be the number the card is actually running
    /// rather than the one a slider about the system's own path happens to be on.
    /// </remarks>
    int OutputFrames => 0;

    /// <summary>
    /// Brings the sound up on the current device, if nothing has yet.
    /// </summary>
    /// <remarks>
    /// Anything that is about to make a sound calls this rather than running an init of its own,
    /// which is what makes one device serve the pads and the tracker both.
    /// </remarks>
    void EnsureInitialized();

    /// <summary>
    /// A pad started, stopped or failed.
    /// </summary>
    /// <remarks>
    /// Raised from whatever thread noticed, which for a stream running out is not the drawing
    /// one. Whoever listens is responsible for getting to their own thread.
    /// </remarks>
    event EventHandler<PadPlaybackChanged>? PadPlaybackChanged;

    /// <summary>Whether that pad is sounding now.</summary>
    bool IsPadPlaying(int padIndex);

    /// <summary>How far through it is, nought to one, or nought when it is not playing.</summary>
    double GetPadProgress(int padIndex);

    /// <summary>How loud that pad is right now, nought to one, for its own meter.</summary>
    float GetPadLevel(int padIndex);

    /// <summary>The level that pad is set to play at, which is the fader and not the meter.</summary>
    float GetPadChannelVolume(int padIndex);

    /// <summary>
    /// Everything this application plays, summed, or a bus that is not open where the switch is
    /// off. See <see cref="IOutputBus"/>.
    /// </summary>
    IOutputBus Output { get; }

    /// <summary>
    /// The pads' own bus, which is one source on <see cref="Output"/> however many pads are down.
    /// </summary>
    /// <remarks>
    /// A sub-bus rather than each pad plugged into the output on its own, because the pads are one
    /// thing to the desk: a fader over this is the pads against the song, and that is what a strip
    /// on the mixer is.
    /// </remarks>
    IOutputBus PadBus { get; }

    /// <summary>The take being auditioned on RECORD, as one source on <see cref="Output"/>.</summary>
    /// <remarks>
    /// Its own bus for the reason the pads have one: there is more than one thing that auditions a
    /// take, the list on RECORD and the editing dialog, and to the desk they are one source.
    /// </remarks>
    IOutputBus TakeBus { get; }

    /// <summary>Plays a file on that pad, from the beginning, at that level.</summary>
    void PlaySample(int padIndex, string filePath, float volume);

    /// <summary>
    /// Plays a stream from the network on that pad.
    /// </summary>
    /// <remarks>
    /// It arrives when it arrives: nothing here waits for it, and the pad reports itself playing
    /// once there is something to play. A stream that cannot be reached comes back through
    /// <see cref="PadPlaybackChanged"/> as an error rather than as silence.
    /// </remarks>
    void PlayStream(int padIndex, string url, float volume);

    /// <summary>Stops that pad, fading out if it has been given a fade.</summary>
    void StopSample(int padIndex);

    /// <summary>What that pad plays: a recording off the shelf, or a stream.</summary>
    void SetPadSource(int padIndex, PadSourceKind kind, string? source);

    /// <summary>Its level, which takes effect at once on whatever it is playing.</summary>
    void SetPadVolume(int padIndex, float volume);

    /// <summary>Whether it starts again when it reaches the end.</summary>
    void SetPadLoop(int padIndex, bool loop);

    /// <summary>How long it takes to come up to level when it starts.</summary>
    void SetPadFadeIn(int padIndex, double seconds);

    /// <summary>And how long it takes to go quiet when it is stopped.</summary>
    void SetPadFadeOut(int padIndex, double seconds);

    /// <summary>
    /// Changes how many pads there are, keeping what still fits.
    /// </summary>
    /// <remarks>
    /// The matrix is a setting, so this happens while the application is running and while pads
    /// are playing. Anything on a pad that is going away stops; everything else is left alone,
    /// including what it is playing.
    /// </remarks>
    void Resize(int newPadCount);

    /// <summary>
    /// Puts an effect in a pad's path, or takes one off with null.
    /// </summary>
    /// <remarks>
    /// The effect hears that pad and nothing else, and it stays with the pad across the next
    /// thing it plays. It is handed blocks on the audio thread, so it must not wait for anything
    /// and must not allocate.
    /// </remarks>
    void SetPadInsert(int padIndex, Plugins.Interfaces.IAudioInsert? insert);

    /// <summary>What is on a pad, or null.</summary>
    Plugins.Interfaces.IAudioInsert? GetPadInsert(int padIndex);

    /// <summary>
    /// The rate a pad's audio runs at, for a plugin that has to match it.
    /// </summary>
    /// <remarks>
    /// A plugin works out its filters from the rate it was given, so it has to be told the rate
    /// of the thing it is actually processing, which for a pad is the file's own rate rather
    /// than the device's.
    /// </remarks>
    int PadSampleRate(int padIndex);
}
