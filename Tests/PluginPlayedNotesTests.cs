using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The notes a plugin plays of its own accord, on their way off the audio thread.
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

}
