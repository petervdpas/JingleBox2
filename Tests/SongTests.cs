using System.Collections.Generic;
using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A song, and the two things a history needs of it: being written down, and being poured back.
/// </summary>
/// <remarks>
/// The tests run in groups. First what a track owns and what happens to it when tracks move or
/// are taken away; then the master, which is a strip without being a track; then the round trip
/// through <see cref="SongStore"/> and <see cref="Song.TakeFrom"/>, which is what an undo step is
/// made of; then a track's insert chain and the plugin patches that travel beside the document
/// rather than inside it.
/// </remarks>
public class SongTests
{
    /// <summary>
    /// A normalised song with two named instruments and a strip per track, which is the least
    /// that makes moving a track or taking an instrument out something that can be seen.
    /// </summary>
    private static Song Made()
    {
        var song = new Song();
        song.Normalize();

        song.Instruments.Add(new TrackerInstrument { Name = "One" });
        song.Instruments.Add(new TrackerInstrument { Name = "Two" });

        foreach (var one in song.Instruments) one.EnsureId();

        while (song.Mix.Count < song.TrackCount) song.Mix.Add(new TrackMix());

        return song;
    }

    /// <summary>
    /// A cursor at a line and track, so the tests read as positions rather than pairs.
    /// </summary>
    private static PatternCursor At(int line, int track = 0) => new() { Line = line, Track = track };

    /// <summary>
    /// What a track plays is the track's own business and nobody else's.
    /// </summary>
    /// <remarks>
    /// The tracker used to answer this with whichever instrument was picked out in the list
    /// beside the pattern when a track had none of its own, so a track with no sound source
    /// sounded somebody else's instrument from the keyboard and wrote its number into cells.
    /// Two different questions, and only one of them is about the track.
    /// </remarks>
    [Fact]
    public void A_track_with_nothing_on_it_plays_nothing()
    {
        var song = Made();

        Assert.Equal(TrackerCell.NoInstrument, song.GetTrackInstrument(1));
        Assert.Null(song.InstrumentAt(song.GetTrackInstrument(1)));
    }

    /// <summary>And the other half of the same question: a track pointed at an instrument plays
    /// that one, by its number in the song's own list.</summary>
    [Fact]
    public void And_a_track_with_something_on_it_plays_that()
    {
        var song = Made();

        song.SetTrackInstrument(1, 1);

        Assert.Equal(1, song.GetTrackInstrument(1));
        Assert.Equal("Two", song.InstrumentAt(song.GetTrackInstrument(1))!.Name);
    }

    /// <summary>
    /// A number that names no instrument reads as nothing rather than as a fault.
    /// </summary>
    /// <remarks>
    /// An instrument taken out of the song leaves the track it was on with nothing, rather than
    /// with a number pointing past the end of the list.
    /// </remarks>
    [Fact]
    public void A_track_pointed_past_the_end_plays_nothing()
    {
        var song = Made();

        song.SetTrackInstrument(1, 9);

        Assert.Equal(TrackerCell.NoInstrument, song.GetTrackInstrument(1));
    }

    /// <summary>
    /// A strip belongs to its track and to no other.
    /// </summary>
    /// <remarks>
    /// The cheapest way for a mixer to be wrong is for two tracks to be handed the same object,
    /// and it is the kind of wrong that looks like magic: two faders that move together.
    /// </remarks>
    [Fact]
    public void Every_track_has_a_strip_of_its_own()
    {
        var song = Made();

        song.Mix[0].Volume = 0.25;
        song.Mix[1].Volume = 1.75;

        Assert.Equal(0.25, song.Mix[0].Volume);
        Assert.Equal(1.75, song.Mix[1].Volume);
        Assert.NotSame(song.Mix[0], song.Mix[1]);
    }

    /// <summary>
    /// Dragging a track carries its instrument, its strip and its automation lane.
    /// </summary>
    /// <remarks>
    /// Everything a track has moves with it. The notes are the obvious half; the mix, the
    /// instrument and the automation are the half that would quietly stay behind and leave the
    /// track playing somebody else's settings.
    /// <para>
    /// What was track 0 is now track 2, with its instrument, its level and its lane. And what it
    /// passed over slid up one place, keeping its own.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_track_moved_takes_everything_it_owns_with_it()
    {
        var song = Made();

        song.SetTrackInstrument(0, 0);
        song.SetTrackInstrument(1, 1);

        song.Mix[0].Volume = 0.25;
        song.Mix[1].Volume = 1.75;

        song.Patterns[0].Lane(new AutomationLane
        {
            Track = 0, Kind = Midi.ControlKind.Mix, Mix = Midi.MixControl.Volume
        });

        Assert.True(song.MoveTrack(0, 2));

        Assert.Equal(0, song.GetTrackInstrument(2));
        Assert.Equal(0.25, song.Mix[2].Volume);
        Assert.Equal(2, song.Patterns[0].Lanes[0].Track);

        Assert.Equal(1, song.GetTrackInstrument(0));
        Assert.Equal(1.75, song.Mix[0].Volume);
    }

    /// <summary>
    /// A ducker's key track is renumbered along with everything else when tracks move.
    /// </summary>
    /// <remarks>
    /// A side chain names the track that pushes it down by number, and those numbers change
    /// under it when a track moves. Left alone it would duck from whatever slid into the place.
    /// </remarks>
    [Fact]
    public void A_side_chain_follows_the_track_it_listens_to()
    {
        var song = Made();

        song.Mix[3].DuckFrom = 0;

        song.MoveTrack(0, 2);

        Assert.Equal(2, song.Mix[3].DuckFrom);
    }

    /// <summary>
    /// The "no key track" value is not a track number and must not be renumbered, or a strip
    /// ducking from nothing would start ducking from track 2.
    /// </summary>
    [Fact]
    public void And_one_listening_to_nothing_goes_on_listening_to_nothing()
    {
        var song = Made();

        song.Mix[3].DuckFrom = TrackMix.NoKey;

        song.MoveTrack(0, 2);

        Assert.Equal(TrackMix.NoKey, song.Mix[3].DuckFrom);
    }

    /// <summary>
    /// Shrinking the track count and growing it again gives a track with nothing on it.
    /// </summary>
    /// <remarks>
    /// A track taken off and put back is a new track, not the old one returning: it has no
    /// instrument, no level anybody set and no lane. Undo is what brings the old one back.
    /// </remarks>
    [Fact]
    public void A_track_taken_off_does_not_leave_its_settings_behind_for_the_next_one()
    {
        var song = Made();

        song.Mix[3].Volume = 0.1;
        song.SetTrackInstrument(3, 1);

        song.TrackCount = 3;
        song.Normalize();

        song.TrackCount = 4;
        song.Normalize();

        Assert.Equal(new TrackMix().Volume, song.Mix[3].Volume);
        Assert.Equal(TrackerCell.NoInstrument, song.GetTrackInstrument(3));
    }

    /// <summary>The mix holds one strip per track and the master is not among them.</summary>
    /// <remarks>
    /// The master is a strip and not a track: it is not in the list of them, so nothing that
    /// walks the tracks can reach it by counting, and it does not move when they are reordered.
    /// </remarks>
    [Fact]
    public void The_master_is_not_one_of_the_tracks()
    {
        var song = Made();

        Assert.Equal(song.TrackCount, song.Mix.Count);
        Assert.DoesNotContain(song.Master, song.Mix);
    }

    /// <summary>
    /// Reordering the tracks reaches no count that could touch the master, so its level survives
    /// a drag on the mixer.
    /// </summary>
    [Fact]
    public void And_stays_where_it_is_when_the_tracks_move()
    {
        var song = Made();

        song.Master.Volume = 0.5;

        song.MoveTrack(0, 3);

        Assert.Equal(0.5, song.Master.Volume);
    }

    /// <summary>
    /// The master is in the song file as its own thing rather than as strip -1 of the track list,
    /// so all three of its settings have to come back.
    /// </summary>
    [Fact]
    public void A_master_written_down_and_read_back_is_the_same_master()
    {
        var song = Made();

        song.Master.Volume = 0.4;
        song.Master.Pan = -0.25;
        song.Master.Mute = true;

        var was = SongStore.Uncopy(SongStore.Copy(song));

        Assert.NotNull(was);
        Assert.Equal(0.4, was!.Master.Volume);
        Assert.Equal(-0.25, was.Master.Pan);
        Assert.True(was.Master.Mute);
    }

    /// <summary>
    /// A song file with no master in it gets one, and it changes nothing about the sound.
    /// </summary>
    /// <remarks>
    /// A song written before the master existed has none in its file, and has to open sounding
    /// exactly as it did: unity, centred, and nothing across it.
    /// </remarks>
    [Fact]
    public void A_song_from_before_the_master_opens_at_unity()
    {
        var song = Made();

        song.Master = null!;
        song.Normalize();

        Assert.NotNull(song.Master);
        Assert.Equal(TrackMix.DefaultVolume, song.Master.Volume);
        Assert.Equal(0, song.Master.Pan);
        Assert.False(song.Master.Mute);
    }

    /// <summary>
    /// The round trip an expensive history step is made of: copy the song to text and pour it
    /// back, with the tempo, the name and the instruments intact.
    /// </summary>
    [Fact]
    public void A_song_written_down_and_read_back_is_the_same_song()
    {
        var song = Made();
        song.Bpm = 174;
        song.Name = "At the races";

        var was = SongStore.Uncopy(SongStore.Copy(song));

        Assert.NotNull(was);
        Assert.Equal(174, was!.Bpm);
        Assert.Equal(2, was.Instruments.Count);
        Assert.Equal("Two", was.Instruments[1].Name);
    }

    /// <summary>
    /// A damaged copy answers nothing rather than faulting, because this reader is also what
    /// opens somebody's file and a bad song must not take the application with it.
    /// </summary>
    [Fact]
    public void Rubbish_reads_back_as_nothing_rather_than_throwing()
    {
        Assert.Null(SongStore.Uncopy("not a song"));
        Assert.Null(SongStore.Uncopy(""));
    }

    /// <summary>
    /// A remembered song is poured into the live one in place: panels and the rack hold the song
    /// they were opened on, so it can never be swapped for another instance.
    /// </summary>
    [Fact]
    public void Taking_from_another_song_keeps_this_ones_identity()
    {
        var live = Made();
        var was = SongStore.Uncopy(SongStore.Copy(live))!;

        was.Bpm = 90;

        live.TakeFrom(was);

        Assert.Equal(90, live.Bpm);
    }

    /// <summary>Pouring a song back keeps the pattern objects, not only the song object.</summary>
    /// <remarks>
    /// The cheap steps in a history hold a pattern by reference. Replacing the list would leave
    /// every one of them pointing at an object the song no longer holds, and undoing a note after
    /// undoing an instrument would appear to do nothing at all.
    /// </remarks>
    [Fact]
    public void And_keeps_the_patterns_identity_too()
    {
        var live = Made();
        var pattern = live.PatternAt(0)!;

        PatternEdit.EnterNote(pattern, At(0), new Note(60), 0);

        var was = SongStore.Uncopy(SongStore.Copy(live))!;

        live.TakeFrom(was);

        Assert.Same(pattern, live.PatternAt(0));
        Assert.True(live.PatternAt(0)![0, 0].Note.IsPlayable);
    }

    /// <summary>
    /// Keeping the identity of the patterns that are there cannot mean refusing to change how
    /// many there are, since adding and removing a pattern is itself an undoable edit.
    /// </summary>
    [Fact]
    public void A_song_with_more_patterns_adds_them_and_one_with_fewer_drops_them()
    {
        var live = Made();
        var more = SongStore.Uncopy(SongStore.Copy(live))!;

        more.AddPattern();
        more.AddPattern();

        live.TakeFrom(more);
        Assert.Equal(3, live.Patterns.Count);

        var fewer = SongStore.Uncopy(SongStore.Copy(Made()))!;
        live.TakeFrom(fewer);

        Assert.Single(live.Patterns);
    }

    /// <summary>
    /// Poured nothing, the song is left exactly as it was, so a step that failed to read cannot
    /// empty the song somebody is working on.
    /// </summary>
    [Fact]
    public void Nothing_at_all_changes_nothing()
    {
        var live = Made();
        live.Bpm = 174;

        live.TakeFrom(null);

        Assert.Equal(174, live.Bpm);
    }

    /// <summary>
    /// The chain is in the strip, so it is in the song file and in every history step that
    /// carries the mix.
    /// </summary>
    [Fact]
    public void A_tracks_insert_chain_travels_with_the_song()
    {
        var live = Made();

        var chain = new PluginChainConfig();
        chain.Devices.Add(new PluginDeviceConfig { Id = "reverb", Name = "Reverb", Path = "/p/reverb.clap" });
        live.Mix[0].Plugins = chain;

        var was = SongStore.Uncopy(SongStore.Copy(live))!;

        Assert.Single(was.Mix[0].Plugins!.Devices);
        Assert.Equal("Reverb", was.Mix[0].Plugins!.Devices[0].Name);
    }

    /// <summary>
    /// A plugin's own patch is saved and read back, which is the fault that made Serum on a track
    /// come back sounding roughly right and calling itself "- Init -": the knobs were saved and
    /// the wavetables, the FX rack and the preset's name were not.
    /// </summary>
    [Fact]
    public void An_effects_own_patch_travels_with_the_song()
    {
        var live = Made();

        var chain = new PluginChainConfig();
        chain.Devices.Add(new PluginDeviceConfig
        {
            Id = "serum",
            Name = "Serum 2 FX",
            Path = "/p/Serum2.vst3",
            State = new byte[] { 9, 8, 7, 6 }
        });

        live.Mix[0].Plugins = chain;
        live.Name = "patched";

        var store = new SongStore();
        string path = store.PathFor(live.Name);

        store.Save(live, path);

        var back = store.Load(path)!;

        Assert.Equal(new byte[] { 9, 8, 7, 6 }, back.Mix[0].Plugins!.Devices[0].State);
    }

    /// <summary>
    /// The patch goes into the zip as <c>state/t00-00.bin</c> rather than into song.json.
    /// </summary>
    /// <remarks>
    /// The patches came out of the document because they are almost all of it, and because a
    /// document is all or nothing: a patch that came back damaged used to cost the song.
    /// </remarks>
    [Fact]
    public void An_effects_patch_is_kept_beside_the_document_and_not_inside_it()
    {
        var live = Made();

        var chain = new PluginChainConfig();
        chain.Devices.Add(new PluginDeviceConfig { Id = "serum", Name = "Serum 2 FX", State = new byte[64] });

        live.Mix[0].Plugins = chain;
        live.Name = "beside";

        var store = new SongStore();
        string path = store.PathFor(live.Name);

        store.Save(live, path);

        using var container = System.IO.Compression.ZipFile.OpenRead(path);

        Assert.NotNull(container.GetEntry("state/t00-00.bin"));

        using var reading = new System.IO.StreamReader(container.GetEntry("song.json")!.Open());

        Assert.DoesNotContain("AAAAAAAA", reading.ReadToEnd());
    }

    /// <summary>
    /// Describing a chain is how two of them are compared, and it has to leave the original
    /// holding its own patches.
    /// </summary>
    /// <remarks>
    /// A plugin asked for its lump twice is under no obligation to answer the same bytes, so
    /// comparing chains with their patches in would report every chain as changed and rebuild all
    /// of them on every undo. The last assertion is the other half: the chain it came from still
    /// has its own.
    /// </remarks>
    [Fact]
    public void A_chain_described_is_the_same_chain_without_the_patches()
    {
        var chain = new PluginChainConfig();
        chain.Devices.Add(new PluginDeviceConfig { Id = "one", Name = "One", State = new byte[] { 1, 2 } });

        var described = chain.Described();

        Assert.Empty(described.Devices[0].State);
        Assert.Equal("One", described.Devices[0].Name);

        Assert.Equal(2, chain.Devices[0].State.Length);
    }

    /// <summary>
    /// A song written before plugin patches were saved opens with an empty lump rather than a
    /// null one, so nothing downstream has to ask whether there is a patch at all.
    /// </summary>
    [Fact]
    public void A_chain_saved_before_patches_existed_reads_back_as_one_without_any()
    {
        var chain = new PluginChainConfig();
        chain.Devices.Add(new PluginDeviceConfig { Id = "old", Name = "Old" });

        var was = SongStore.Uncopy(SongStore.Copy(WithChain(chain)))!;

        Assert.Empty(was.Mix[0].Plugins!.Devices[0].State);
    }

    /// <summary>
    /// A song with one chain on its first track, which is all the chain tests need.
    /// </summary>
    private static Song WithChain(PluginChainConfig chain)
    {
        var song = Made();
        song.Mix[0].Plugins = chain;
        return song;
    }

    /// <summary>
    /// Instruments are named in a cell by their place in the list, so removing one has to walk
    /// every pattern and pull the numbers above it down.
    /// </summary>
    [Fact]
    public void Taking_an_instrument_out_renumbers_every_note_that_named_one_after_it()
    {
        var song = Made();

        PatternEdit.EnterNote(song.PatternAt(0)!, At(0), new Note(60), instrument: 1);

        song.RemoveInstrumentAt(0);

        Assert.Equal(0, song.PatternAt(0)![0, 0].Instrument);
    }
}

/// <summary>What the mix adds up to, mute and solo included.</summary>
public class MixLevelTests
{
    /// <summary>Four strips at their defaults: audible, unmuted and unsoloed.</summary>
    private static List<TrackMix> Four() => new() { new(), new(), new(), new() };

    /// <summary>
    /// The ordinary case, and the one that has to cost nothing: no solo anywhere.
    /// </summary>
    [Fact]
    public void With_nothing_soloed_every_track_is_audible()
    {
        var mix = Four();

        Assert.False(MixLevels.AnySolo(mix));

        for (int track = 0; track < 4; track++) Assert.True(MixLevels.IsAudible(mix, track));
    }

    /// <summary>Mute silences its own strip and no other.</summary>
    [Fact]
    public void A_muted_track_is_not()
    {
        var mix = Four();
        mix[1].Mute = true;

        Assert.False(MixLevels.IsAudible(mix, 1));
        Assert.True(MixLevels.IsAudible(mix, 0));
    }

    /// <summary>
    /// Solo is about the whole mix rather than one strip: one raised anywhere silences every
    /// strip that is not.
    /// </summary>
    [Fact]
    public void One_track_soloed_silences_the_rest()
    {
        var mix = Four();
        mix[2].Solo = true;

        Assert.True(MixLevels.AnySolo(mix));
        Assert.True(MixLevels.IsAudible(mix, 2));
        Assert.False(MixLevels.IsAudible(mix, 0));
    }

    /// <summary>
    /// Mute wins over solo, since the two can be left set at once and silence is the answer
    /// nobody can mistake for a fault.
    /// </summary>
    [Fact]
    public void A_track_that_is_soloed_and_muted_is_muted()
    {
        var mix = Four();
        mix[0].Solo = true;
        mix[0].Mute = true;

        Assert.False(MixLevels.IsAudible(mix, 0));
    }

    /// <summary>
    /// A track number with no strip behind it, and no mix at all, both answer audible: the mixer
    /// is asked while a song is being opened and must not silence what it cannot yet see.
    /// </summary>
    [Fact]
    public void A_track_the_mix_has_never_heard_of_is_left_alone()
    {
        Assert.True(MixLevels.IsAudible(Four(), 99));
        Assert.True(MixLevels.IsAudible(null, 0));
    }

    /// <summary>
    /// What an instrument is, in one sentence, lives on the instrument and not on either of the
    /// two things that print it.
    /// </summary>
    /// <remarks>
    /// Said in two: the song's instrument list and the block at the head of a track's chain.
    /// Worked out in one, because two copies of this sentence would drift. A plugin has no machine
    /// of ours to name, so it says what it is instead.
    /// </remarks>
    [Fact]
    public void An_instrument_says_what_it_is_in_one_place()
    {
        var synth = new TrackerInstrument { Name = "Mine", Kind = TrackerInstrumentKind.Synth };

        Assert.StartsWith(synth.Machine.Name, synth.Detail);
        Assert.Contains(synth.Patch.Wave.ToString().ToLowerInvariant(), synth.Detail);

        var plugin = new TrackerInstrument
        {
            Name = "Mine", Kind = TrackerInstrumentKind.Plugin, PluginName = "Serum 2"
        };

        Assert.Equal("Serum 2", plugin.Detail);
    }
}
