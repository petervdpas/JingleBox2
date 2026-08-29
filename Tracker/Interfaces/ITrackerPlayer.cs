using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker.Enums;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Plays a song: the clock, the voices, and the plugins each track is holding.
/// </summary>
/// <remarks>
/// What a line means is <see cref="ITrackerSequencer"/>'s business and is arithmetic over the
/// pattern. What it sounds like is this, and the two are kept apart so the first can be put a
/// question to without a sound card.
///
/// The clock runs on a thread of its own against a stopwatch, with each step's time worked out
/// from the start rather than added to the last one. A timer that sleeps "one step" at a time
/// gathers its own lateness, and over a sixty four line pattern that drift is audible.
///
/// Almost everything here is raised or called from a thread that is not the drawing one, and
/// nothing here marshals: a listener gets to its own thread itself. That is deliberate, since
/// the one thread that must never wait is the clock.
///
/// It also owns the plugins, one process each, and that is most of the size of it. A plugin is
/// slow to start, holds its own notes, has a release to finish, and must be put down when a song
/// is closed or it goes on running with nothing holding it.
/// </remarks>
public interface ITrackerPlayer : IDisposable
{
    /// <summary>Raised from the clock thread on every step. Marshal before touching UI.</summary>
    event EventHandler<TrackerPosition>? PositionChanged;

    /// <summary>Raised on every transport change, from whichever thread caused it.</summary>
    event EventHandler<TrackerTransportState>? StateChanged;

    /// <summary>Raised when a pass ends, whether it was stopped or ran off the end.</summary>
    event EventHandler? Stopped;

    /// <summary>
    /// Raised for every note that goes to a track, so a panel can show what its track plays.
    /// </summary>
    /// <remarks>
    /// From the clock thread, like the position. It carries the track and the note and nothing
    /// else: what a listener does with it is its own business, and one that needs the instrument
    /// can ask the song for it. An OFF row is a note this track played too, and the one it says
    /// is that there is not one, so a panel showing its keys puts them out on hearing it.
    ///
    /// The length is nought for a note in a pattern, which lasts until whatever the track plays
    /// next and that has not happened yet. An audition carries its real length instead, which is
    /// what lets a key light and a cursor run for exactly as long as the sound.
    /// </remarks>
    event EventHandler<(int Track, Note Note, double Seconds)>? NotePlayed;

    /// <summary>Playing, paused or stopped.</summary>
    TrackerTransportState State { get; }

    /// <summary>Whether the clock is running.</summary>
    bool IsPlaying { get; }

    /// <summary>Whether it is stopped somewhere it can be continued from.</summary>
    bool IsPaused { get; }

    /// <summary>The step last played, which is what a playhead follows.</summary>
    TrackerPosition Position { get; }

    /// <summary>Whether it is walking the order list or staying on one pattern.</summary>
    TrackerPlayMode Mode { get; }

    /// <summary>Start again from the top instead of stopping at the end.</summary>
    bool Loop { get; set; }

    /// <summary>Instrument files that could not be loaded, for reporting after a take.</summary>
    IReadOnlyCollection<string> FailedInstruments { get; }

    /// <summary>
    /// What writes the automation lanes, when there is anything for them to write through.
    /// </summary>
    /// <remarks>
    /// Handed in rather than made here, because resolving a lane means knowing the whole
    /// program: which machine a track plays, which plugins are in its chain, where its fader is.
    /// The player knows the clock and the voices and deliberately nothing else. Null is ordinary
    /// and means a song plays exactly as it did before any of this existed.
    /// </remarks>
    AutomationPlayer? Automation { get; set; }

    /// <summary>Starts a song from that step, walking the order or staying on one pattern.</summary>
    /// <remarks>
    /// Whatever was running is taken down first, the recordings are read up front so the first
    /// note is not late, and the mix is pushed once before the clock starts: a side chain set
    /// while stopped has nowhere to go until there is a song to take it.
    /// </remarks>
    void Play(Song song, TrackerPosition from, TrackerPlayMode mode = TrackerPlayMode.Song);

    /// <summary>Continues from where a pause left off. Does nothing when not paused.</summary>
    void Resume();

    /// <summary>Freezes at the current step. The voices are cut, the position is kept.</summary>
    void Pause();

    /// <summary>Stops, and goes back to the top.</summary>
    void Stop();

    /// <summary>
    /// Stops what an instrument is sounding by hand.
    /// </summary>
    /// <remarks>
    /// For leaving a machine's panel: what you played on it is its own, and hearing it go on
    /// under the next machine's picture, with that picture's cursor running to it, is one
    /// instrument wearing another's face. A pattern's notes are untouched.
    /// </remarks>
    void CutPreview(TrackerInstrument? instrument);

    /// <summary>
    /// Lets go of one note played by hand, which is what a key coming up means.
    /// </summary>
    /// <remarks>
    /// The same thing a pattern's OFF does to a track, done to one auditioned note. A key is
    /// down while a hand is on it and up when the hand comes off, and what it started releases
    /// then rather than running to the end of the file.
    /// </remarks>
    void LetPreview(TrackerInstrument? instrument, Note note);

    /// <summary>Sounds a single note, for auditioning while editing. Independent of playback.</summary>
    /// <param name="instrument">What to play it on, which need not be in any song.</param>
    /// <param name="note">The key that was pressed.</param>
    /// <param name="gain">How hard, on top of the instrument's own level.</param>
    /// <param name="track">
    /// Which strip it sounds on, or below nought for a note that belongs to no track. The
    /// tracker's keyboard names the track the cursor is in, so a note played by hand goes
    /// through that track's inserts and its fader and moves its meter and the master's, which
    /// is what makes it tell you what the part will sound like. The rack's keyboard names none,
    /// because the instrument it is playing may not be in any song.
    /// </param>
    /// <returns>
    /// How long the note will sound, so a keyboard can light its key and a picture can run its
    /// cursor for exactly that long. Zero when nothing sounded.
    /// </returns>
    /// <remarks>
    /// Auditions pile up, as a keyboard does, unless the instrument says it is one voice, which
    /// cuts what it was sounding first. A generated sound holds for a fixed moment, since no key
    /// is being let go of; a recording holds for its own length, because a take cut off part way
    /// through is not the sound the instrument makes.
    /// </remarks>
    double Preview(TrackerInstrument instrument, Note note, float gain = 1f, int track = -1);

    /// <summary>
    /// How loud a strip is right now, both sides, for the mixer's meters. Zero for one that is
    /// not sounding, and zero for every strip before there is a mixer at all.
    /// </summary>
    /// <remarks>
    /// The master is a strip here like any other and reads what is leaving rather than what any
    /// track is doing, which makes it the one meter on the page measuring what you actually
    /// hear.
    /// </remarks>
    (float Left, float Right) LevelFor(int track);

    /// <summary>What the engine is running at, which is what a plugin here has to be built for.</summary>
    int SampleRate { get; }

    /// <summary>
    /// Asks the engine to run at a rate, or at the device's own. Only heard before the first
    /// note, so it comes from settings when the tracker is built.
    /// </summary>
    void UseSampleRate(int rate);

    /// <summary>
    /// How far ahead of the sound card to mix, in milliseconds. Heard when the stream is
    /// opened, so it comes from settings when the tracker is built.
    /// </summary>
    void UseRenderAhead(int milliseconds);

    /// <summary>
    /// The chain of effects on a strip, made and put into the mix the first time it is asked
    /// for. A strip with nothing on it costs an empty chain, which does nothing per block.
    /// </summary>
    /// <remarks>
    /// Below nought is the master, which is a strip without being a track. Asking for one starts
    /// the engine, because an effect that is never handed a block cannot work on the audio,
    /// cannot finish a delay's tail, and cannot tell the host what its own window did.
    /// </remarks>
    PluginChain ChainFor(int track);

    /// <summary>
    /// Writes every strip's chain into the song, ready to be saved with it.
    /// </summary>
    /// <param name="song">The song to write into.</param>
    /// <param name="patches">
    /// Whether to read each plugin's own state as well as its knobs. True where the song is
    /// about to be written down, which is every caller: a plugin asked for its patch is a round
    /// trip to another process, and that is a price worth paying at a save and nowhere else.
    /// </param>
    void CaptureChains(Song song, bool patches = true);

    /// <summary>
    /// Builds every strip's chain from what the song holds. Returns the plugins it could not
    /// find, so the song can say so rather than quietly sounding different.
    /// </summary>
    IReadOnlyList<string> RestoreChains(Song song);

    /// <summary>
    /// Makes the loaded chains match what the song now says, for the strips where they differ.
    /// </summary>
    /// <remarks>
    /// For a history putting a step back. A track's inserts live in two places: the song holds a
    /// description of them, which is what a step carries, and the mixer holds the plugins
    /// themselves, each in a process of its own. Restoring the description alone leaves the
    /// picture and the sound disagreeing, which is worse than not restoring it at all.
    ///
    /// Only where they differ, and that is the whole reason this exists beside
    /// <see cref="RestoreChains"/>. Rebuilding a chain means stopping every plugin in it and
    /// starting them again, which is seconds a plugin. Almost every undo changes no chain at
    /// all, and pays one comparison; only undoing a plugin change pays the reload, which is the
    /// one case where anybody expects a pause.
    /// </remarks>
    /// <returns>Which strips were rebuilt, so their panels can be told.</returns>
    IReadOnlyList<int> MatchChains(Song song);

    /// <summary>
    /// Moves a track to another position, live: the plugins loaded on it, the effects inserted
    /// on it, and the levels its notes were last set to.
    /// </summary>
    /// <remarks>
    /// The song has already been reordered by the time this is called. This is the running half
    /// of the same move, and the two have to agree or the notes arrive at one track while the
    /// sound answers on another.
    /// </remarks>
    void MoveTrack(int from, int to);

    /// <summary>
    /// Opens the audio engine if it is not already open. A plugin has to be built for the rate
    /// the engine settled on, and until the device is open there is no rate to build for.
    /// </summary>
    void EnsureEngine();

    /// <summary>
    /// The plugin a track plays, loaded if it is not already, and null when the plugin is
    /// missing or this host cannot play its kind.
    /// </summary>
    /// <remarks>
    /// The same one the notes go to, deliberately: a second copy would be a second sound, and
    /// turning a knob on it would change something nobody can hear.
    /// </remarks>
    IPluginInstrument? EnsurePlayerOn(int track, TrackerInstrument instrument);

    /// <summary>
    /// Starts every plugin this song's tracks are set to, at once, without waiting for any of
    /// them.
    /// </summary>
    /// <remarks>
    /// A song used to start its plugins one note at a time, on the clock, which is the worst
    /// possible moment and the worst possible thread: the first bar of a song with three plugins
    /// in it stuttered three times, once per track, each stall the length of a plugin starting
    /// up. Opening the song is the moment nobody is listening, and each plugin is its own
    /// process, so there is nothing to be gained by starting them in a queue.
    ///
    /// Nothing here is waited on. What this leaves is a song whose plugins are on their way; a
    /// note that arrives before its own plugin is up waits for that one, as it always did, and
    /// not for the others.
    /// </remarks>
    void PreloadPlugins(Song song);

    /// <summary>
    /// The plugin a track is playing, without loading one. What the editor asks when it wants to
    /// save a patch back.
    /// </summary>
    IPluginInstrument? PlayerOn(int track);

    /// <summary>
    /// The plugin behind an audition, loaded if it is not already the one being auditioned. Also
    /// what the editor calls to get a live plugin to work on.
    /// </summary>
    /// <remarks>
    /// One copy of a plugin, not two. Where a track is already playing this instrument, that is
    /// the copy that comes back: a second one is a second process holding a second set of
    /// wavetables, and a knob turned on it would change something nobody can hear.
    /// </remarks>
    IPluginInstrument? PreviewPlayerFor(TrackerInstrument instrument);

    /// <summary>Puts the auditioned plugin down, for a page that is being left.</summary>
    void ClearPreviewPlayer();

    /// <summary>Takes every plugin off the tracks and puts it down. For closing a song.</summary>
    void ClearPlayers();

    /// <summary>
    /// Puts down every plugin this song is holding: the instruments and the inserts both.
    /// </summary>
    /// <remarks>
    /// For leaving the tracker behind. Each plugin is a process with its patch loaded, and a
    /// song of four is four of them sitting there while you work on the pads, keeping the engine
    /// running besides, because a track with an insert has to be given blocks.
    ///
    /// What is loaded is read back onto the song first. A plugin's own window is where most of
    /// its settings are turned, and letting go without capturing would throw away everything
    /// since the last save, which is the sort of loss nobody would connect to changing tabs.
    /// </remarks>
    void LetGoOfPlugins(Song song);

    /// <summary>Picks them all up again, for coming back to the tracker.</summary>
    /// <remarks>
    /// The chains first, because a track's inserts are what the engine needs to run at all, and
    /// then the instruments side by side, as opening a song does.
    /// </remarks>
    void TakeUpPlugins(Song song);

    /// <summary>
    /// Re-applies the mix to whatever is sounding, for a fader, a mute or a side chain moved
    /// mid-take. The note's own level is kept, so the two are combined rather than one replacing
    /// the other.
    /// </summary>
    void ApplyMix();

    /// <summary>Forgets a cached recording so an edited or re-recorded file is picked up.</summary>
    void ReloadInstrument(string filePath);

    /// <summary>How far through its recording a sounding sample voice is, or -1 for none.</summary>
    double SamplePosition(int track);

    /// <summary>What this player's stream is putting out, 0 to 1.</summary>
    double OutputLevel { get; }
}
