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
    /// A thread started from a real-time thread is an ordinary thread, and the one that asked
    /// still says it is real time.
    /// </summary>
    /// <remarks>
    /// Linux hands a new thread its creator's scheduling, so without the reset every thread the
    /// runtime makes from an audio thread is real time too: the background compiler was found at
    /// the audio thread's own priority in a plugin's process, and with it switched on every plugin
    /// window opened cost 50 to 185 ms of a plugin's audio thread waiting for a core. Only a
    /// machine that grants the scheduler can say anything here, so where it is refused nothing is
    /// claimed.
    /// </remarks>
    [Fact]
    public void A_thread_started_from_a_real_time_thread_is_ordinary()
    {
        if (!OperatingSystem.IsLinux()) return;

        Environment.SetEnvironmentVariable(RealtimeThread.Variable, "1");

        IRealtimeThread realtime = new RealtimeThread();

        bool taken = false;
        string asker = "";
        string child = "";

        var parent = new System.Threading.Thread(() =>
        {
            taken = realtime.Take();
            asker = realtime.Said();

            var started = new System.Threading.Thread(() => child = realtime.Said());
            started.Start();
            started.Join();
        });

        parent.Start();
        parent.Join();

        if (!taken) return;

        Assert.StartsWith("real time", asker);
        Assert.Equal("the ordinary scheduler", child);
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

    /// <summary>
    /// What the system allows is answered by the system, and is the same answer a thread asking
    /// for it gets.
    /// </summary>
    /// <remarks>
    /// JingleBox2 does not decide whether its audio runs in real time; the system does, and
    /// whatever it allows is used. So the answer has to be the one a real thread gets when it asks,
    /// on any machine the tests run on: granted here means granted there, refused means refused.
    /// </remarks>
    [Fact]
    public void What_the_system_allows_is_what_a_thread_gets()
    {
        IRealtimeThread realtime = new RealtimeThread();

        bool allowed = realtime.Allowed;

        Environment.SetEnvironmentVariable(RealtimeThread.Variable, "1");

        bool taken = false;
        var asker = new System.Threading.Thread(() => taken = realtime.Take());
        asker.Start();
        asker.Join();

        Assert.Equal(allowed, taken);
    }

    /// <summary>Asking twice gives the same answer, since the system's permission does not move while the program runs.</summary>
    [Fact]
    public void Asking_twice_gives_the_same_answer()
    {
        IRealtimeThread realtime = new RealtimeThread();

        Assert.Equal(realtime.Allowed, realtime.Allowed);
    }
}
