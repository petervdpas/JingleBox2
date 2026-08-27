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
