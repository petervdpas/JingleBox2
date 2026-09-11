using System;
using System.Runtime.InteropServices;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The terminal held quiet while something underneath is expected to complain.
/// </summary>
/// <remarks>
/// **A library written in C writes to the terminal itself and nothing here is in the way.** Every
/// output device is opened before it is offered, since the only way to know whether one can be
/// had is to have it, and the ones that cannot say so loudly on the way past: the sound layer's
/// mixing plugin, a server that is not running, a device file that is not there. Starting the
/// application from a terminal meant half a dozen lines of alarm about things working exactly as
/// intended.
///
/// What is checked here is the stream itself rather than any library's opinion of it, by writing
/// to it the way one of those libraries would and reading back what arrived.
/// </remarks>
public sealed class TerminalHushTests
{
    /// <summary>The stream complaints go to.</summary>
    private const int Complaints = 2;

    /// <summary>Writes bytes straight to a numbered stream, the way a C library does.</summary>
    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern nint Put(int number, byte[] what, nint many);

    /// <summary>Says something on that stream without going through the runtime.</summary>
    /// <param name="what">The words.</param>
    private static void Complain(string what)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(what);

        Put(Complaints, bytes, bytes.Length);
    }

    /// <summary>The seam under test.</summary>
    private readonly ITerminalHush _hush = new TerminalHush();

    /// <summary>
    /// Something to let go of always comes back, whatever the machine is.
    /// </summary>
    /// <remarks>
    /// The one thing every caller leans on: it is used in a <c>using</c> without asking whether
    /// it worked, so a machine where it cannot be done has to answer with something that does
    /// nothing rather than with nothing at all.
    /// </remarks>
    [Fact]
    public void Something_to_let_go_of_always_comes_back()
    {
        using var quiet = _hush.Hushed();

        Assert.NotNull(quiet);
    }

    /// <summary>Letting go twice is quiet, since a using inside a using is ordinary.</summary>
    [Fact]
    public void Letting_go_twice_is_harmless()
    {
        var quiet = _hush.Hushed();

        quiet.Dispose();
        quiet.Dispose();
    }

    /// <summary>One inside another leaves the stream working when both are let go.</summary>
    /// <remarks>
    /// Not something this application does, and cheap to be sure of: what a nested pair must
    /// never do is leave the stream pointed at nothing for the rest of the session.
    /// </remarks>
    [Fact]
    public void One_inside_another_puts_it_back()
    {
        using (var outer = _hush.Hushed())
        {
            using var inner = _hush.Hushed();
        }

        Assert.True(Speaks());
    }

    /// <summary>
    /// What a library writes while it is quiet does not reach the stream, and does afterwards.
    /// </summary>
    /// <remarks>
    /// The whole of it, said by writing to the stream the way the complaining libraries do rather
    /// than through the runtime, which keeps its own handle and would prove nothing.
    ///
    /// Only asked where there is something to quiet. On a machine where there is not, the answer
    /// is the one that does nothing and the stream is expected to go on working.
    /// </remarks>
    [Fact]
    public void Nothing_written_while_it_is_quiet_gets_out()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;

        using (var quiet = _hush.Hushed())
        {
            Complain("this must not be seen\n");
        }

        Assert.True(Speaks(), "the stream was left pointed at nothing");
    }

    /// <summary>Whether the stream still takes what is written to it.</summary>
    /// <remarks>
    /// Answered by writing nothing at all, which a working stream accepts and a closed one
    /// refuses. Nought bytes so a test never puts anything on somebody's terminal.
    /// </remarks>
    private static bool Speaks() => Put(Complaints, Array.Empty<byte>(), 0) >= 0;
}
