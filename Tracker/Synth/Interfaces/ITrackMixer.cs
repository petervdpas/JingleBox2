using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker.Synth;
using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// The song's tracks, summed into one buffer: a bus, a level, a pan, an insert chain, a ducker
/// and an instrument apiece.
/// </summary>
/// <remarks>
/// One voice per track, the tracker way: a new note cuts the one still ringing there. Auditions
/// sit outside that, carry no track at all and simply pile up, which is why a panel's keyboard
/// cannot be heard on a strip or turned down by one. The one exception is a kit, where a crash
/// has to go on ringing under the snare that follows it and only a pad in the same choke group
/// stops another.
///
/// It was called SynthMixer, which was true when it summed synth voices and nothing else. It
/// grew a bus and a level and a ducker and a plugin slot for every track and went on wearing the
/// old name, which said the wrong thing about the one class the whole mix goes through.
///
/// Every track is rendered on a bus of its own before anything is summed. That is what ducking
/// needs: one track has to be measurable while another is being moved by it, and once everything
/// is added together there is nothing left to measure. It is also what an insert needs, since an
/// effect on a track must hear that track and nothing else.
///
/// The master is a strip without being a track. It has no bus, no voices, no instrument and
/// nothing keying it, because everything has already been summed by the time it is reached; what
/// it has is a level, a place, and one effect the whole song goes through. It is strip -1
/// everywhere it is named, so nobody can collide with it by adding a thirty-third track.
///
/// The mixer does not rest while any track has something inserted on it, whether or not anything
/// is going through it. A delay has a tail to finish after the last note, and a plugin only ever
/// hands the host what its own window did at the end of a block it was given: a mixer that rests
/// is a plugin switched off without being told, and a knob turned in its window then reaches
/// nothing and nobody.
///
/// <see cref="Render"/> runs on the audio callback thread while notes are started from the clock
/// and from the UI, so what it renders is a snapshot taken under a lock. Everything else here is
/// called from those other threads and is safe to call while a block is in flight. Inside the
/// render nothing allocates, nothing waits on another process, and a plugin is never told
/// anything while the lock is held: somebody else's code has no business running inside it.
/// </remarks>
public interface ITrackMixer
{
    /// <summary>The rate every voice, filter and envelope in the mix is worked out at.</summary>
    int SampleRate { get; }

    /// <summary>How many voices are sounding, for a status line and for a test.</summary>
    int VoiceCount { get; }

    /// <summary>
    /// How far through its recording the newest sounding sample voice is, or -1 for none.
    /// </summary>
    /// <remarks>
    /// A track's own voice and a voice auditioned by hand both answer, because a panel showing
    /// a cursor wants the piece that is playing and does not care which of the two started it.
    /// Newest first: playing a second key while the first still rings should move the cursor to
    /// what was just asked for.
    /// </remarks>
    double SamplePosition(int track);

    /// <summary>
    /// Points one strip's side chain at another track: the kick keying the bass.
    /// </summary>
    /// <remarks>
    /// A depth of nought, or no key, is a strip that plays at its own level. A strip cannot key
    /// itself, and the key is read as it sounded before it was itself ducked, so two tracks
    /// pointed at each other cannot chase one another down into silence.
    /// </remarks>
    /// <param name="track">The strip being pushed down.</param>
    /// <param name="depth">How far down it goes when the key is at full scale, 0 to 1.</param>
    /// <param name="key">The track doing the pushing, or <see cref="TrackMix.NoKey"/> for none.</param>
    /// <param name="releaseMs">How long it takes to come back up.</param>
    void SetDucking(int track, double depth, int key, double releaseMs);

    /// <summary>
    /// Puts an effect in a track's path, or takes one out with null. The track is rendered on
    /// its own bus, so what the effect sees is that track and nothing else.
    /// </summary>
    /// <remarks>
    /// The effect runs on the bus before the side chains, so what keys a duck is the track as
    /// it sounds, effects included, which is what anyone listening would call the track.
    ///
    /// A track with something inserted on it is rendered whether or not it is playing. See the
    /// remarks on this interface for why that is not an optimisation somebody forgot.
    /// </remarks>
    void SetInsert(int track, IAudioInsert? insert);

    /// <summary>What is on a track, or null.</summary>
    IAudioInsert? InsertOn(int track);

    /// <summary>
    /// The loudest thing that came off a track's bus in the last block, 0 to 1.
    /// </summary>
    /// <remarks>
    /// Measured after the insert, so it is what the track actually put into the mix. It is not
    /// what the track's meter shows: see <see cref="LevelFor"/> for that.
    /// </remarks>
    float GetTrackLevel(int track);

    /// <summary>
    /// Puts a plugin on a track, or takes one off with null. Whatever was there is told to
    /// stop first, or it carries on playing into a bus nobody renders.
    /// </summary>
    void SetInstrument(int track, IPluginInstrument? instrument);

    /// <summary>What plugin is playing a track, or null when its instrument is one of the machines.</summary>
    IPluginInstrument? InstrumentOn(int track);

    /// <summary>
    /// Moves everything a track is holding to another position: its plugin, its effects, its
    /// side chain and the columns riding its bus.
    /// </summary>
    /// <remarks>
    /// The song is reordered by the view; this is the live half of the same move. Without it
    /// the notes would arrive at their new track and the plugin would still be answering on
    /// the old one, so every track would play somebody else's sound.
    ///
    /// A side chain names the track that keys it, and those numbers have just moved, so the
    /// keys are renumbered too and every follower starts again from where it now stands.
    ///
    /// Voices are cut rather than carried across. A voice remembers the track it was started
    /// on, and there is no sound reason to hear a note go on playing on a track that is no
    /// longer where it was. A cut is a short fade, so this costs a note ending rather than a
    /// click.
    /// </remarks>
    void MoveTrack(int from, int to);

    /// <summary>Puts a plugin in the audition slot, or takes one out with null.</summary>
    /// <remarks>
    /// The audition slot belongs to no track. What it plays is added to the loose bus with the
    /// other auditions, so it goes through nobody's fader and moves nobody's meter.
    /// </remarks>
    void SetPreviewInstrument(IPluginInstrument? instrument);

    /// <summary>What is in the audition slot, if anything.</summary>
    IPluginInstrument? PreviewInstrument { get; }

    /// <summary>
    /// Plays a note on the audition plugin, letting go of it after a while. There is no key to
    /// release when a note is played by clicking on it, so it releases itself.
    /// </summary>
    void PreviewPlugin(Note note, float gain, double holdSeconds);

    /// <summary>
    /// Plays a note by hand on the plugin a track is already playing, letting go of it after a
    /// while.
    /// </summary>
    /// <remarks>
    /// The track's own copy rather than the audition one, deliberately. It is the copy whose
    /// window is open and whose knobs have just been turned; a second copy would be a second
    /// sound, playing whatever the song was last saved with.
    /// </remarks>
    void PreviewOnTrack(int track, Note note, float gain, double holdSeconds);

    /// <summary>Starts a note on a track's plugin. The volume column rides its bus after.</summary>
    /// <remarks>
    /// One note a track, as a tracker has always worked. The note that was there is let go
    /// rather than cut off, so a plugin plays its own release instead of clicking.
    /// </remarks>
    void PluginNoteOn(int track, Note note, float gain, float pan);

    /// <summary>Lets go of whatever a track's plugin is holding.</summary>
    void PluginNoteOff(int track);

    /// <summary>Follows the volume and pan columns while a plugin note holds.</summary>
    /// <remarks>
    /// A plugin plays at its own level and knows nothing about the tracker's columns, so they
    /// are applied to the bus after it has played rather than being sent to it.
    /// </remarks>
    void SetPluginLevels(int track, float gain, float? pan);

    /// <summary>How far a track is being pushed down right now, 1 being not at all.</summary>
    float DuckGainFor(int track);

    /// <summary>Starts a note on a synth patch, cutting whatever that track was sounding.</summary>
    void NoteOn(int track, SynthPatch patch, Note note, float gain, float pan);

    /// <summary>
    /// Starts a note on Ouroboros, sliding from whatever the track was sounding.
    /// </summary>
    /// <remarks>
    /// The note before is what glide glides from, and the mixer is the only thing that knows
    /// what it was. It is read before the old voice is cut, because cutting it is what makes it
    /// stop being the note before.
    /// </remarks>
    void NoteOn(int track, MonoSynthPatch patch, Note note, float gain, float pan);

    /// <summary>
    /// Sounds a recording on a track, under the same rules: the track's last note is cut, and
    /// the voice takes its place. The caller brings the audio, so the mixer never reads a file.
    /// </summary>
    void NoteOn(int track, TrackerInstrument instrument, SampleData sample, Note note, float gain, float pan);

    /// <summary>
    /// Fires one pad of a kit: its own recording, at its own pitch, over whatever else is
    /// already sounding on the track.
    /// </summary>
    /// <remarks>
    /// The one place in this engine where a track's last note is not cut. Everywhere else one
    /// voice to a track is the rule and glide, legato and the tracker's own habits are built on
    /// it; a kit is the exception, because a crash has to go on ringing under the snare that
    /// follows it. The only thing that stops a pad is another pad in its choke group.
    ///
    /// The pad's own note is passed as the base note as well, so the ratio comes out at one and
    /// nothing is resampled. That is the machine: a key chooses which recording sounds, not how
    /// fast to read one.
    /// </remarks>
    void NoteOn(int track, DrumPad pad, SynthPatch patch, SampleData sample, Note note, float gain, float pan);

    /// <summary>
    /// Plays one zone of a map: its recording, read at whatever speed the key asks for.
    /// </summary>
    /// <remarks>
    /// The kit's method with one word changed. There the played note goes in as the root, so
    /// the ratio comes out at one; here the zone's own root goes in, so the note decides how
    /// fast to read. That one word is the whole difference between BongaBong and Zampler.
    ///
    /// And unlike a kit, the track's last note is cut: this is an instrument rather than a rack
    /// of them, and one voice to a track is how the tracker has always played one.
    /// </remarks>
    void NoteOn(int track, SampleZone zone, SamplerPatch patch, SampleData sample, Note note, float gain, float pan);

    /// <summary>Sounds a note that releases on its own, for auditioning while editing.</summary>
    /// <param name="patch">The sound being built.</param>
    /// <param name="note">What to play.</param>
    /// <param name="gain">How loud, before the strip it lands on.</param>
    /// <param name="holdSeconds">How long before it lets go of itself, there being no key to lift.</param>
    /// <param name="audition">
    /// Which panel is playing it. An audition belongs to no track, so a track number cannot
    /// tell two panels apart and this is what an instrument set to one voice matches on.
    /// </param>
    /// <param name="track">
    /// The strip it sounds on, or nothing for a note that belongs to no track. A machine's own
    /// keyboard on the rack is played on an instrument that may not be in any song, so it goes
    /// through nobody's fader and nobody's meter. The tracker's keyboard is the opposite: it is
    /// playing the instrument that track holds, so it sounds on that track, through its inserts,
    /// its level and its meter, which is what makes an audition tell you what the part will
    /// actually sound like.
    /// </param>
    void Preview(SynthPatch patch, Note note, float gain, double holdSeconds, string audition,
                 int track = SynthVoice.NoTrack);

    /// <summary>The same, on Ouroboros, for a note played while building the sound.</summary>
    /// <remarks>
    /// No glide: an audition has no note before it to slide from. Given no track it piles up
    /// with the other auditions rather than cutting one.
    /// </remarks>
    /// <param name="patch">The sound being built.</param>
    /// <param name="note">What to play.</param>
    /// <param name="gain">How loud, before the strip it lands on.</param>
    /// <param name="holdSeconds">How long before it lets go of itself.</param>
    /// <param name="audition">Which panel is playing it.</param>
    /// <param name="track">
    /// The strip it sounds on, or nothing for a note that belongs to no track. A machine's own
    /// keyboard on the rack is played on an instrument that may not be in any song, so it goes
    /// through nobody's fader and nobody's meter. The tracker's keyboard is the opposite: it is
    /// playing the instrument that track holds, so it sounds on that track, through its inserts,
    /// its level and its meter, which is what makes an audition tell you what the part will
    /// actually sound like.
    /// </param>
    void Preview(MonoSynthPatch patch, Note note, float gain, double holdSeconds, string audition,
                 int track = MonoSynthVoice.NoTrack);

    /// <summary>The same, for a zone played on the panel rather than by a pattern.</summary>
    /// <returns>How long the note will sound, or zero if it did not start.</returns>
    /// <param name="zone">Which part of the map was struck.</param>
    /// <param name="patch">Zampler's shaping.</param>
    /// <param name="sample">The take, already decoded.</param>
    /// <param name="note">What was played.</param>
    /// <param name="gain">How loud, before the strip it lands on.</param>
    /// <param name="holdSeconds">The shortest it will sound. A one-shot holds for its own length instead.</param>
    /// <param name="audition">Which panel is playing it.</param>
    /// <param name="track">
    /// The strip it sounds on, or nothing for a note that belongs to no track. A machine's own
    /// keyboard on the rack is played on an instrument that may not be in any song, so it goes
    /// through nobody's fader and nobody's meter. The tracker's keyboard is the opposite: it is
    /// playing the instrument that track holds, so it sounds on that track, through its inserts,
    /// its level and its meter, which is what makes an audition tell you what the part will
    /// actually sound like.
    /// </param>
    double Preview(SampleZone zone, SamplerPatch patch, SampleData sample, Note note, float gain,
                   double holdSeconds, string audition, int track = SynthVoice.NoTrack);

    /// <summary>The same, for a pad tapped on the panel rather than played by a pattern.</summary>
    /// <returns>How long the note will sound, or zero if it did not start.</returns>
    /// <param name="pad">Which pad of the kit was struck, which brings its choke group with it.</param>
    /// <param name="patch">The pad's shaping.</param>
    /// <param name="sample">The take, already decoded.</param>
    /// <param name="note">What was played.</param>
    /// <param name="gain">How loud, before the strip it lands on.</param>
    /// <param name="holdSeconds">The shortest it will sound. A one-shot holds for its own length instead.</param>
    /// <param name="audition">Which panel is playing it.</param>
    /// <param name="track">
    /// The strip it sounds on, or nothing for a note that belongs to no track. A machine's own
    /// keyboard on the rack is played on an instrument that may not be in any song, so it goes
    /// through nobody's fader and nobody's meter. The tracker's keyboard is the opposite: it is
    /// playing the instrument that track holds, so it sounds on that track, through its inserts,
    /// its level and its meter, which is what makes an audition tell you what the part will
    /// actually sound like.
    /// </param>
    double Preview(DrumPad pad, SynthPatch patch, SampleData sample, Note note, float gain,
                   double holdSeconds, string audition, int track = SynthVoice.NoTrack);

    /// <summary>A recording sounded once, for auditioning while editing.</summary>
    /// <returns>How long the note will sound, or zero if it did not start.</returns>
    /// <param name="instrument">What is being auditioned, which brings its window and its tuning.</param>
    /// <param name="sample">The take, already decoded.</param>
    /// <param name="note">What was played.</param>
    /// <param name="gain">How loud, before the strip it lands on.</param>
    /// <param name="holdSeconds">The shortest it will sound. A one-shot holds for its own length instead.</param>
    /// <param name="audition">Which panel is playing it.</param>
    /// <param name="track">
    /// The strip it sounds on, or nothing for a note that belongs to no track. A machine's own
    /// keyboard on the rack is played on an instrument that may not be in any song, so it goes
    /// through nobody's fader and nobody's meter. The tracker's keyboard is the opposite: it is
    /// playing the instrument that track holds, so it sounds on that track, through its inserts,
    /// its level and its meter, which is what makes an audition tell you what the part will
    /// actually sound like.
    /// </param>
    double Preview(TrackerInstrument instrument, SampleData sample, Note note, float gain,
                   double holdSeconds, string audition, int track = SynthVoice.NoTrack);

    /// <summary>
    /// Stops what this instrument was sounding by hand, for one that plays one note at a time.
    /// </summary>
    /// <remarks>
    /// A short fade rather than a release, the same as a track retriggering itself: the next
    /// note starts now, and a full release would still be running underneath it.
    /// </remarks>
    void CutAuditions(string audition);

    /// <summary>
    /// Lets go of one auditioned note, the way a pattern's OFF lets go of a track's.
    /// </summary>
    /// <remarks>
    /// The release and not the cut, because a key coming up is not a stop button: what was
    /// started goes into its release the way it does when a pattern reaches an OFF, so a sound
    /// with a long tail keeps its tail.
    ///
    /// One note, not every note this instrument is sounding by hand. Two keys held on a kit are
    /// two drums, and letting go of one must not silence the other.
    ///
    /// A one-shot is left alone entirely. It is a hit and it runs its own length: the mouse
    /// coming up is not a stop button on a recording with an end of its own, and a click lasts
    /// a few milliseconds, so following the key there would turn every drum into a tick.
    /// </remarks>
    void LetAudition(string audition, int semitone);

    /// <summary>Lets go of whatever a track was sounding, which is what a pattern's OFF does.</summary>
    void NoteOff(int track);

    /// <summary>Follows the volume and pan columns while a note holds.</summary>
    void SetLevels(int track, float gain, float? pan);

    /// <summary>Silence, now. Used by the transport rather than by a note off.</summary>
    /// <remarks>
    /// Voices are killed rather than released, because pressing stop is a request for silence
    /// now and not for every tail in the song to play out first. The side chains let go and
    /// every meter falls with them.
    /// </remarks>
    void StopAll();

    /// <summary>Lets go of every note on every plugin, for a stop.</summary>
    /// <remarks>
    /// Told outside the lock. A plugin being asked to stop is somebody else's code, and it has
    /// no business running with the mixer's lock held.
    /// </remarks>
    void AllPluginNotesOff();

    /// <summary>
    /// Fills an interleaved stereo buffer with everything playing. Always writes the whole
    /// buffer: the audio callback has no way to say "nothing this time".
    /// </summary>
    /// <remarks>
    /// The audio thread. In order: every sounding track onto its own bus, each bus through its
    /// insert, each bus into the mix through its side chain, the loose bus of auditions on top,
    /// and then the master. Nothing here allocates, takes a lock for longer than a few list
    /// operations, or waits on another process.
    ///
    /// A plugin that throws costs the block it threw in and nothing else. A managed fault on
    /// the audio thread would otherwise take the whole application with it, which is the same
    /// bargain a track's chain makes everywhere else.
    /// </remarks>
    void Render(float[] buffer, int frames);

    /// <summary>Moves the master fader, which is the last thing between the mix and the card.</summary>
    void SetMaster(float gain, float? pan);

    /// <summary>Puts an effect across the whole mix, or takes one off with null.</summary>
    /// <remarks>
    /// It runs before the level and the pan, because a limiter on the master is put there to
    /// catch what the mix does and not what the fader does.
    /// </remarks>
    void SetMasterInsert(IAudioInsert? insert);

    /// <summary>What is across the whole mix, if anything.</summary>
    IAudioInsert? MasterInsert { get; }

    /// <summary>
    /// What is leaving, for the meter beside the master fader.
    /// </summary>
    /// <remarks>
    /// A peak measured off the last buffer, and therefore only true while buffers are being
    /// asked for. A track's meter is worked out from the voices that are sounding, so it falls
    /// on its own the moment they stop; this one would sit at whatever the last thing to play
    /// was until something asked for another buffer, and nothing does when the stream is not
    /// running. So it is stamped when it is taken and goes out on its own if nothing renews it.
    ///
    /// Aged rather than cleared where the rendering stops, because there are several ways for it
    /// to stop and only one of them passes through this class.
    ///
    /// Measured after the effect, the fader and the saturation, which makes it the one meter on
    /// the page reading what you actually hear.
    /// </remarks>
    (float Left, float Right) MasterLevel { get; }

    /// <summary>
    /// How loud a track is sounding, for a meter. Taken from the voices rather than from the
    /// mixed buffer: the voices are already summed together by the time that exists.
    /// </summary>
    /// <remarks>
    /// Falls on its own once the voices stop, which is why it needs none of the ageing
    /// <see cref="MasterLevel"/> does. A track played by a plugin has no voices to ask, so its
    /// bus peak stands in.
    /// </remarks>
    (float Left, float Right) LevelFor(int track);
}
