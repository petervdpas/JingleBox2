using System;
using System.Runtime.InteropServices;
using JingleBox2.Audio;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The sound server is told this application is stereo, before anything is opened.
/// </summary>
/// <remarks>
/// **The library opens a device as wide as the card claims speakers and nothing here ever uses
/// more than two.** On an interface set to a surround profile that came out as four channels
/// going out and six coming in, so the machine's own patchbay showed a stream shaped like
/// somebody's speaker arrangement with two of it filled and the rest silent, and which two those
/// were was left to whatever mapped them.
///
/// The server's ALSA layer reads its properties out of the environment and applies the width to
/// the hardware parameters the device reports, so told two, the device offers two and the library
/// has nothing else to choose. Measured on a real machine rather than read and hoped for:
/// <c>aplay -D pipewire --dump-hw-params</c> answers <c>CHANNELS: [1 64]</c> on its own and
/// <c>CHANNELS: 2</c> with this set.
///
/// What is here is the bookkeeping around it, since the layer itself needs a sound server: that
/// it is said, that it is said only where there is something to say, and that a machine which has
/// already been told something of its own is left alone.
/// </remarks>
public sealed class SoundServerWidthTests : IDisposable
{
    /// <summary>What the server's ALSA layer reads.</summary>
    private const string Variable = "PIPEWIRE_ALSA";

    /// <summary>What it held before a test, so the machine is left as it was found.</summary>
    private readonly string? _before = Environment.GetEnvironmentVariable(Variable);

    /// <summary>Takes one out of the real environment, which the runtime's own call cannot.</summary>
    /// <remarks>
    /// **The process's environment and the runtime's table of it are two different things**,
    /// which is the whole of what the class under test exists to bridge: setting one through the
    /// runtime is invisible to a library written in C. So clearing between tests has to reach the
    /// same place, or the second test finds what the first left and reads it as something
    /// somebody else set.
    /// </remarks>
    /// <param name="name">The variable.</param>
    /// <returns>Nought where it was taken out.</returns>
    [DllImport("libc", EntryPoint = "unsetenv", CharSet = CharSet.Ansi)]
    private static extern int Take(string name);

    /// <summary>Clears it in both places, so a test starts from nothing.</summary>
    private static void Clear()
    {
        Environment.SetEnvironmentVariable(Variable, null);

        try
        {
            Take(Variable);
        }
        catch (Exception)
        {
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The environment belongs to the process rather than to a test, and these run one at a time
    /// in it, so what was there is put back rather than cleared.
    /// </remarks>
    public void Dispose()
    {
        Clear();

        if (_before != null) Environment.SetEnvironmentVariable(Variable, _before);
    }

    /// <summary>On a machine with the server, the width is said.</summary>
    [Fact]
    public void It_is_said_where_there_is_a_server()
    {
        Clear();

        var width = new SoundServerWidth(linux: true);

        Assert.True(width.Ask());
        Assert.Contains("alsa.channels=2", Environment.GetEnvironmentVariable(Variable));
    }

    /// <summary>And nothing whatever is said anywhere else.</summary>
    /// <remarks>
    /// Windows has no such server, and putting one of its variables into the environment there
    /// would be this application leaving something behind that means nothing on that machine.
    /// </remarks>
    [Fact]
    public void Nothing_is_said_where_there_is_none()
    {
        Clear();

        var width = new SoundServerWidth(linux: false);

        Assert.False(width.Ask());
        Assert.Null(Environment.GetEnvironmentVariable(Variable));
    }

    /// <summary>
    /// A machine that has already been told something is left exactly as it was.
    /// </summary>
    /// <remarks>
    /// Somebody who has set this themselves has said something more particular than this knows
    /// how to, and writing over it would be this application arguing with its own owner.
    /// </remarks>
    [Fact]
    public void What_somebody_else_said_is_left_alone()
    {
        Environment.SetEnvironmentVariable(Variable, "{ alsa.rate=48000 }");

        var width = new SoundServerWidth(linux: true);

        Assert.False(width.Ask());
        Assert.Equal("{ alsa.rate=48000 }", Environment.GetEnvironmentVariable(Variable));
        Assert.Contains("not ours to change", width.Said(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Saying it twice says it once, since the second time it is already there.</summary>
    /// <remarks>
    /// Which is what a second window, or a start that got as far as this and turned back, would
    /// do. The answer is no the second time and the value is what the first one put there.
    /// </remarks>
    [Fact]
    public void Saying_it_twice_leaves_what_the_first_said()
    {
        Clear();

        Assert.True(new SoundServerWidth(linux: true).Ask());
        Assert.False(new SoundServerWidth(linux: true).Ask());
        Assert.Contains("alsa.channels=2", Environment.GetEnvironmentVariable(Variable));
    }

    /// <summary>What was arranged is said in words, and they differ by what happened.</summary>
    [Fact]
    public void What_happened_is_said_in_words()
    {
        Clear();

        var said = new SoundServerWidth(linux: true);

        said.Ask();

        Assert.Contains("two channels", said.Said(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(said.Said(), new SoundServerWidth(linux: false).Said());
    }
}
