using System;
using JingleBox2.Audio.Plugins.Bridge;
using JingleBox2.Audio.Plugins.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The notes a plugin played, crossing the shared block from the plugin's process to this one.
/// </summary>
/// <remarks>
/// Both sides work out where they are from the frame count in the header, so this opens the same
/// block twice, as the two processes do, and writes with one and reads with the other.
/// </remarks>
public class BridgePlayedTests
{
    /// <summary>What one side writes, the other side reads.</summary>
    [Fact]
    public void Played_notes_cross_the_block()
    {
        var mine = BridgeBlock.Create(512, out string path);

        try
        {
            var theirs = BridgeBlock.Open(path);

            Assert.NotNull(theirs);

            theirs!.WritePlayed(new[]
            {
                new PlayedNote(0, 10, 36, 1f, true),
                new PlayedNote(128, 10, 38, 0.5f, false),
            });

            var into = new PlayedNote[8];

            Assert.Equal(2, mine.Played(into));
            Assert.Equal(new PlayedNote(0, 10, 36, 1f, true), into[0]);
            Assert.Equal(new PlayedNote(128, 10, 38, 0.5f, false), into[1]);

            theirs.WritePlayed(ReadOnlySpan<PlayedNote>.Empty);

            Assert.Equal(0, mine.Played(into));

            theirs.Dispose();
        }
        finally
        {
            mine.Dispose();

            try { System.IO.File.Delete(path); } catch (Exception) { }
        }
    }
}
