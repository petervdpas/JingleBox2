using System.Collections.Generic;
using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A song, and the two things a history needs of it: being written down, and being poured back.
/// </summary>
public class SongTests
{
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

    [Fact]
    public void And_a_track_with_something_on_it_plays_that()
    {
        var song = Made();

        song.SetTrackInstrument(1, 1);

        Assert.Equal(1, song.GetTrackInstrument(1));
        Assert.Equal("Two", song.InstrumentAt(song.GetTrackInstrument(1))!.Name);
    }

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

    /// <remarks>
    /// Everything a track has moves with it. The notes are the obvious half; the mix, the
    /// instrument and the automation are the half that would quietly stay behind and leave the
    /// track playing somebody else's settings.
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

        // What was track 0 is now track 2, with its instrument, its level and its lane.
        Assert.Equal(0, song.GetTrackInstrument(2));
        Assert.Equal(0.25, song.Mix[2].Volume);
        Assert.Equal(2, song.Patterns[0].Lanes[0].Track);

        // And what it passed over slid up one place, keeping its own.
        Assert.Equal(1, song.GetTrackInstrument(0));
        Assert.Equal(1.75, song.Mix[0].Volume);
    }

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

    [Fact]
    public void And_one_listening_to_nothing_goes_on_listening_to_nothing()
    {
        var song = Made();

        song.Mix[3].DuckFrom = TrackMix.NoKey;

        song.MoveTrack(0, 2);

        Assert.Equal(TrackMix.NoKey, song.Mix[3].DuckFrom);
    }

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

    [Fact]
    public void And_stays_where_it_is_when_the_tracks_move()
    {
        var song = Made();

        song.Master.Volume = 0.5;

        song.MoveTrack(0, 3);

        Assert.Equal(0.5, song.Master.Volume);
    }

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

    [Fact]
    public void Rubbish_reads_back_as_nothing_rather_than_throwing()
    {
        Assert.Null(SongStore.Uncopy("not a song"));
        Assert.Null(SongStore.Uncopy(""));
    }

    [Fact]
    public void Taking_from_another_song_keeps_this_ones_identity()
    {
        var live = Made();
        var was = SongStore.Uncopy(SongStore.Copy(live))!;

        was.Bpm = 90;

        live.TakeFrom(was);

        Assert.Equal(90, live.Bpm);
    }

    [Fact]
    public void And_keeps_the_patterns_identity_too()
    {
        // The cheap steps in a history hold a pattern by reference. Replacing the list would
        // leave every one of them pointing at an object the song no longer holds, and undoing a
        // note after undoing an instrument would appear to do nothing at all.
        var live = Made();
        var pattern = live.PatternAt(0)!;

        PatternEdit.EnterNote(pattern, At(0), new Note(60), 0);

        var was = SongStore.Uncopy(SongStore.Copy(live))!;

        live.TakeFrom(was);

        Assert.Same(pattern, live.PatternAt(0));
        Assert.True(live.PatternAt(0)![0, 0].Note.IsPlayable);
    }

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

    [Fact]
    public void Nothing_at_all_changes_nothing()
    {
        var live = Made();
        live.Bpm = 174;

        live.TakeFrom(null);

        Assert.Equal(174, live.Bpm);
    }

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

    [Fact]
    public void A_chain_described_is_the_same_chain_without_the_patches()
    {
        var chain = new PluginChainConfig();
        chain.Devices.Add(new PluginDeviceConfig { Id = "one", Name = "One", State = new byte[] { 1, 2 } });

        var described = chain.Described();

        Assert.Empty(described.Devices[0].State);
        Assert.Equal("One", described.Devices[0].Name);

        // And the chain it came from still has its own.
        Assert.Equal(2, chain.Devices[0].State.Length);
    }

    [Fact]
    public void A_chain_saved_before_patches_existed_reads_back_as_one_without_any()
    {
        var chain = new PluginChainConfig();
        chain.Devices.Add(new PluginDeviceConfig { Id = "old", Name = "Old" });

        var was = SongStore.Uncopy(SongStore.Copy(WithChain(chain)))!;

        Assert.Empty(was.Mix[0].Plugins!.Devices[0].State);
    }

    private static Song WithChain(PluginChainConfig chain)
    {
        var song = Made();
        song.Mix[0].Plugins = chain;
        return song;
    }

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
    private static List<TrackMix> Four() => new() { new(), new(), new(), new() };

    [Fact]
    public void With_nothing_soloed_every_track_is_audible()
    {
        var mix = Four();

        Assert.False(MixLevels.AnySolo(mix));

        for (int track = 0; track < 4; track++) Assert.True(MixLevels.IsAudible(mix, track));
    }

    [Fact]
    public void A_muted_track_is_not()
    {
        var mix = Four();
        mix[1].Mute = true;

        Assert.False(MixLevels.IsAudible(mix, 1));
        Assert.True(MixLevels.IsAudible(mix, 0));
    }

    [Fact]
    public void One_track_soloed_silences_the_rest()
    {
        var mix = Four();
        mix[2].Solo = true;

        Assert.True(MixLevels.AnySolo(mix));
        Assert.True(MixLevels.IsAudible(mix, 2));
        Assert.False(MixLevels.IsAudible(mix, 0));
    }

    [Fact]
    public void A_track_that_is_soloed_and_muted_is_muted()
    {
        var mix = Four();
        mix[0].Solo = true;
        mix[0].Mute = true;

        Assert.False(MixLevels.IsAudible(mix, 0));
    }

    [Fact]
    public void A_track_the_mix_has_never_heard_of_is_left_alone()
    {
        Assert.True(MixLevels.IsAudible(Four(), 99));
        Assert.True(MixLevels.IsAudible(null, 0));
    }

    [Fact]
    public void An_instrument_says_what_it_is_in_one_place()
    {
        // Said in two: the song's instrument list and the block at the head of a track's chain.
        // Worked out in one, because two copies of this sentence would drift.
        var synth = new TrackerInstrument { Name = "Mine", Kind = TrackerInstrumentKind.Synth };

        Assert.StartsWith(synth.Machine.Name, synth.Detail);
        Assert.Contains(synth.Patch.Wave.ToString().ToLowerInvariant(), synth.Detail);

        var plugin = new TrackerInstrument
        {
            Name = "Mine", Kind = TrackerInstrumentKind.Plugin, PluginName = "Serum 2"
        };

        // A plugin has no machine of ours to name, so it says what it is instead.
        Assert.Equal("Serum 2", plugin.Detail);
    }
}
