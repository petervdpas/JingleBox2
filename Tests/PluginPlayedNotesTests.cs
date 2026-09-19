using System;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The notes a plugin plays of its own accord: how they are carried off the audio thread, and
/// where a track says they should go.
/// </summary>
public class PluginPlayedNotesTests
{
    /// <summary>What is put in comes out in the order it went in, with the track it came from.</summary>
    [Fact]
    public void Notes_come_back_in_the_order_they_were_played()
    {
        var waiting = new PlayedNotes();

        waiting.Took(2, new[]
        {
            new PlayedNote(0, 10, 36, 1f, true),
            new PlayedNote(64, 10, 38, 0.5f, true),
        });

        waiting.Took(3, new[] { new PlayedNote(0, 10, 36, 0, false) });

        var into = new TrackPlayedNote[8];

        Assert.Equal(3, waiting.Take(into));

        Assert.Equal(new TrackPlayedNote(2, new PlayedNote(0, 10, 36, 1f, true)), into[0]);
        Assert.Equal(new TrackPlayedNote(2, new PlayedNote(64, 10, 38, 0.5f, true)), into[1]);
        Assert.Equal(new TrackPlayedNote(3, new PlayedNote(0, 10, 36, 0, false)), into[2]);

        Assert.Equal(0, waiting.Take(into));
    }

    /// <summary>Taking fewer than are waiting leaves the rest for the next time.</summary>
    [Fact]
    public void What_is_not_taken_waits()
    {
        var waiting = new PlayedNotes();

        waiting.Took(0, new[]
        {
            new PlayedNote(0, 1, 60, 1f, true),
            new PlayedNote(1, 1, 61, 1f, true),
        });

        var one = new TrackPlayedNote[1];

        Assert.Equal(1, waiting.Take(one));
        Assert.Equal(60, one[0].Note.Note);

        Assert.Equal(1, waiting.Take(one));
        Assert.Equal(61, one[0].Note.Note);
    }

    /// <summary>A queue nobody is emptying drops what will not fit rather than growing.</summary>
    /// <remarks>
    /// It is filled on the audio thread, where asking for memory is the one thing that must not
    /// happen, and a note nobody has taken in a second has gone stale anyway.
    /// </remarks>
    [Fact]
    public void A_full_queue_drops_what_will_not_fit()
    {
        var waiting = new PlayedNotes();
        var many = new PlayedNote[1000];

        for (int at = 0; at < many.Length; at++) many[at] = new PlayedNote(at, 1, 60, 1f, true);

        waiting.Took(0, many);

        var into = new TrackPlayedNote[1000];
        int took = waiting.Take(into);

        Assert.True(took is > 0 and < 1000);
        Assert.Equal(0, into[0].Note.Frame);
    }

    /// <summary>Forgetting empties it, which is what a transport stop does.</summary>
    [Fact]
    public void Forgetting_empties_it()
    {
        var waiting = new PlayedNotes();

        waiting.Took(0, new[] { new PlayedNote(0, 1, 60, 1f, true) });
        waiting.Forget();

        Assert.Equal(0, waiting.Take(new TrackPlayedNote[4]));
    }

    /// <summary>A strip keeps where its plugin's notes go, and a copy of it says the same.</summary>
    [Fact]
    public void A_strip_carries_where_its_plugin_plays()
    {
        var strip = new TrackMix { PluginNotesTo = 3 };

        Assert.Equal(3, strip.Clone().PluginNotesTo);

        strip.PluginNotesTo = -7;
        strip.Clamp();

        Assert.Equal(TrackMix.NoPluginNotes, strip.PluginNotesTo);
    }

    /// <summary>The word picked in the block says which track, and the words come back again.</summary>
    [Fact]
    public void The_block_says_where_the_notes_go()
    {
        var strip = new TrackMix();
        var block = new TrackMidiViewModel(strip, Array.Empty<string>(), Array.Empty<string>(),
                                           _ => { }, () => { }, track: 0, tracks: 3);

        Assert.Equal(TrackMidiViewModel.NoNotes, block.PluginNotesTo);
        Assert.Equal(new[] { TrackMidiViewModel.NoNotes, TrackMidiViewModel.ToInserts, "Track 2", "Track 3" },
                     block.PluginTargets);

        block.PluginNotesTo = "Track 3";

        Assert.Equal(2, strip.PluginNotesTo);
        Assert.Equal("Track 3", block.PluginNotesTo);

        block.PluginNotesTo = TrackMidiViewModel.ToInserts;

        Assert.Equal(TrackMix.PluginNotesToInserts, strip.PluginNotesTo);

        /* Its own track is not offered and is refused if asked for, since a plugin playing its
           own track would be playing itself. */
        block.PluginNotesTo = "Track 1";

        Assert.Equal(TrackMix.NoPluginNotes, strip.PluginNotesTo);
    }
}
