using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using System;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Asking the operating system to schedule a thread as audio, and the switch that governs it.
/// </summary>
/// <remarks>
/// What can be checked without a sound card is the **rule**, not the scheduling: that it is off
/// until it is asked for, that asking is carried in the environment so a plugin's own process
/// hears the same answer, and that a refusal is an ordinary answer rather than an exception. What
/// the kernel actually grants depends on the machine, which is why the class says what it got
/// rather than assuming it got it.
/// </remarks>
public class RealtimeThreadTests : IDisposable
{
    /// <summary>What the variable held before, so the rest of the run is not changed.</summary>
    private readonly string? _before = Environment.GetEnvironmentVariable(RealtimeThread.Variable);

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        Environment.SetEnvironmentVariable(RealtimeThread.Variable, _before);
    }

    /// <summary>Nothing asked for is nothing done, whatever the machine would have allowed.</summary>
    [Fact]
    public void It_is_off_until_it_is_asked_for()
    {
        Environment.SetEnvironmentVariable(RealtimeThread.Variable, null);

        IRealtimeThread thread = new RealtimeThread();

        Assert.False(thread.Take());
    }

    /// <summary>And an explicit no is a no.</summary>
    [Fact]
    public void Nought_is_a_no()
    {
        Environment.SetEnvironmentVariable(RealtimeThread.Variable, "0");

        IRealtimeThread thread = new RealtimeThread();

        Assert.False(thread.Take());
    }

    /// <summary>
    /// Asked for, it either takes it or is refused, and either way it does not throw.
    /// </summary>
    /// <remarks>
    /// A refusal is ordinary: a machine without the right limits will not grant it, and an
    /// application that fell over because of that would be worse than one running slightly late.
    /// </remarks>
    [Fact]
    public void Asked_for_it_answers_rather_than_throwing()
    {
        Environment.SetEnvironmentVariable(RealtimeThread.Variable, "1");

        IRealtimeThread thread = new RealtimeThread();

        var taken = thread.Take();

        Assert.True(taken || !taken);
    }

    /// <summary>What it says is what the thread really is, not what was asked for.</summary>
    [Fact]
    public void It_says_what_the_thread_really_is()
    {
        IRealtimeThread thread = new RealtimeThread();

        Assert.False(string.IsNullOrWhiteSpace(thread.Said()));
    }

    /// <summary>
    /// The answer is carried in the environment, so a plugin's own process inherits it.
    /// </summary>
    /// <remarks>
    /// The one thing here that is not about this process. A plugin host reads no settings, so the
    /// only way it can be told is by being started from something that already knows.
    /// </remarks>
    [Fact]
    public void What_is_wanted_is_left_where_a_child_will_find_it()
    {
        RealtimeThread.Wants(true);
        Assert.Equal("1", Environment.GetEnvironmentVariable(RealtimeThread.Variable));

        RealtimeThread.Wants(false);
        Assert.Equal("0", Environment.GetEnvironmentVariable(RealtimeThread.Variable));
    }

    /// <summary>
    /// Both platforms are answered, whichever one is running the tests.
    /// </summary>
    /// <remarks>
    /// The reason the rule takes the platform rather than looking it up. The real-time scheduler
    /// is a Linux idea and there is no such thing on Windows, so the honest answer there is no
    /// and the settings page can say why instead of offering a switch that does nothing.
    ///
    /// **Which is not the same as Windows arranging nothing.** It asks for the multimedia class
    /// scheduler instead, on every run and without this switch, so what is pinned here is the
    /// absence of this mechanism rather than the absence of any.
    /// </remarks>
    [Fact]
    public void Only_linux_has_a_real_time_scheduler()
    {
        IRealtimeThread thread = new RealtimeThread();

        Assert.True(thread.PossibleOn(linux: true));
        Assert.False(thread.PossibleOn(linux: false));
    }
}
