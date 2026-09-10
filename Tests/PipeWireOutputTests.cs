using System;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// This application as a node on the sound server's own graph, asked without one.
/// </summary>
/// <remarks>
/// **The arrangement is the driver's, said again with a different puller**, which is the whole
/// reason it is worth having in this shape: a driver owns the card and pulls, so the library is
/// opened on its own silent device and the mix is made a decoding stream for it to take. The
/// server is that same arrangement. Everything above the seam therefore already knows how to
/// behave, and what is asked here is the part that has to be true where there is no server:
/// every answer comes back as a plain no rather than taking the audio down with it.
///
/// **That is not a gap in the testing, it is the case worth pinning.** Windows has no such server
/// and is the machine this must never disturb; a Linux machine without it running behaves
/// identically. What cannot be exercised here is a node really appearing on a graph, which needs
/// a graph.
/// </remarks>
public sealed class PipeWireOutputTests
{
    /// <summary>The seam under test.</summary>
    private readonly IPipeWireOutput _pipe = new PipeWireOutput();

    /// <summary>
    /// Whether it is here is said the same way every time it is asked.
    /// </summary>
    /// <remarks>
    /// Remembered rather than asked again, since the answer cannot change while the program runs
    /// and asking means reaching for a library that may not be there.
    /// </remarks>
    [Fact]
    public void The_answer_does_not_change_under_it()
    {
        bool first = _pipe.Present;

        for (int again = 0; again < 5; again++) Assert.Equal(first, _pipe.Present);
    }

    /// <summary>There is a reason to show exactly when there is nothing here.</summary>
    /// <remarks>
    /// The invariant rather than either half of it, so this says something on every machine: a
    /// reason is what being absent means, and having one while the server is there would be a
    /// page explaining away something it can see.
    /// </remarks>
    [Fact]
    public void There_is_a_reason_exactly_when_there_is_nothing_here()
    {
        Assert.Equal(_pipe.Present, _pipe.Missing.Length == 0);
    }

    /// <summary>Nonsense is refused, and refused before anything is reached for.</summary>
    /// <remarks>
    /// Every one of these is turned away by the argument guard, which sits above the library, so
    /// it is safe to run on a machine whose graph somebody else is using.
    /// </remarks>
    [Fact]
    public void Nonsense_is_refused()
    {
        Assert.False(_pipe.Open(0, 44100));
        Assert.False(_pipe.Open(1, 0));
        Assert.False(_pipe.Open(0, 0));
        Assert.False(_pipe.Open(1, -1));
    }

    /// <summary>Nothing is open until something opens, and refusing opens nothing.</summary>
    [Fact]
    public void Nothing_refused_leaves_anything_open()
    {
        Assert.False(_pipe.IsOpen);

        _pipe.Open(0, 44100);

        Assert.False(_pipe.IsOpen);
    }

    /// <summary>Closing what was never opened does nothing, and does it quietly.</summary>
    /// <remarks>
    /// Said on every device change, whatever was running, so it has to be safe to say to
    /// something that has never been asked for anything.
    /// </remarks>
    [Fact]
    public void Closing_what_was_never_open_is_quiet()
    {
        _pipe.Close();
        _pipe.Close();

        Assert.False(_pipe.IsOpen);
    }

    /// <summary>
    /// Without the server there is nothing to play into, and it says so.
    /// </summary>
    /// <remarks>
    /// The case CI runs on both platforms and the one that matters most: a machine with no server
    /// is the ordinary Windows machine, and what it must never do is fail rather than answer.
    /// Asserted rather than skipped, so it says something wherever it applies.
    /// </remarks>
    [Fact]
    public void Without_a_server_there_is_nothing_and_it_says_so()
    {
        if (_pipe.Present) return;

        Assert.NotEmpty(_pipe.Missing);
        Assert.False(_pipe.Open(1, 44100));
        Assert.False(_pipe.IsOpen);
    }

    /// <summary>And on a machine that is not Linux, the reason says which machine it is about.</summary>
    /// <remarks>
    /// Two silences that are not the same silence: a platform that has no such thing, and a Linux
    /// machine where it is simply not running. Only one of them is anybody's to act on.
    /// </remarks>
    [Fact]
    public void The_reason_names_which_kind_of_absence_it_is()
    {
        if (OperatingSystem.IsLinux()) return;

        Assert.False(_pipe.Present);
        Assert.Contains("Linux", _pipe.Missing, StringComparison.OrdinalIgnoreCase);
    }
}
