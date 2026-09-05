using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a note key means while other keys are already down.
/// </summary>
/// <remarks>
/// The letter rows repeat while a key is held, which is wanted: it is how a column is filled.
/// What a repeat must not do is sound the note again, and that is the case this exists for.
///
/// It was found in a log rather than by reading. Holding one key took the mixer from one voice
/// to forty eight in two seconds, each alive for ten, and what reached the master summed to 4.34
/// where full scale is one: four times too much into the saturation, and the collector stopping
/// every thread for 345 ms of one five second window trying to keep up with the churn. The
/// quiet windows either side read two per cent and nothing collected, so the machine was never
/// the problem.
/// </remarks>
public class NotePressTests
{
    /// <summary>The rule under test.</summary>
    private readonly INotePress _press = new NotePress();

    /// <summary>A key that was not down is a note: sound it and write it.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void A_fresh_key_sounds_and_writes(int held)
    {
        Assert.Equal(NoteWant.SoundAndWrite, _press.Wants(again: false, held));
    }

    /// <summary>
    /// A key repeating on its own writes and sounds nothing.
    /// </summary>
    /// <remarks>
    /// This is the one that cost the crackle. The note it would sound is already sounding, since
    /// the first press started a voice and the key has not come up, so a second one is the same
    /// note twice, thirty times a second, each alive for ten seconds.
    /// </remarks>
    [Fact]
    public void A_repeat_writes_and_does_not_sound()
    {
        Assert.Equal(NoteWant.Write, _press.Wants(again: true, held: 1));
    }

    /// <summary>
    /// And a repeat under a chord does nothing at all.
    /// </summary>
    /// <remarks>
    /// There it is a hand resting rather than somebody filling a column, and every repeat would
    /// spray a single note down the pattern under the chord that was just written.
    /// </remarks>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void A_repeat_under_a_chord_does_nothing(int held)
    {
        Assert.Equal(NoteWant.Nothing, _press.Wants(again: true, held));
    }

    /// <summary>
    /// Nothing but a fresh key ever sounds, which is the whole of what stops voices stacking.
    /// </summary>
    /// <remarks>
    /// Said as a sweep rather than case by case, so a fourth answer added later cannot quietly
    /// start sounding on a repeat.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(16)]
    public void Only_a_fresh_key_ever_sounds(int held)
    {
        Assert.NotEqual(NoteWant.SoundAndWrite, _press.Wants(again: true, held));
    }
}
