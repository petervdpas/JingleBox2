using System;
using System.Runtime.InteropServices;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed partial class RealtimeThread : IRealtimeThread
{
    /// <summary>
    /// The environment this is carried in, so a plugin's own process hears the same answer.
    /// </summary>
    /// <remarks>
    /// The setting lives in the settings file, which a plugin host process has no business
    /// reading: it loads one plugin and knows nothing else about this application. So the choice
    /// is put into the environment once, at startup, and the host inherits it the way it already
    /// inherits the trace switch and the log folder.
    ///
    /// It stays readable from outside for the same reason it was useful today: something that
    /// puts a thread ahead of everything on the machine wants a way out that does not need the
    /// settings page to open.
    /// </remarks>
    public const string Variable = "JB_REALTIME";

    /// <summary>
    /// Whether to ask at all, which is no until <c>JB_REALTIME=1</c> says otherwise.
    /// </summary>
    /// <remarks>
    /// **Off until it has been listened to**, which is the rule this application keeps for every
    /// change to the audio path. Asking the operating system to put a thread ahead of everything
    /// else on the machine is not a thing to switch on for somebody, and the last time it went in
    /// beside three other changes at once the sound came apart and nobody could say which of the
    /// four had done it.
    ///
    /// It is read from the environment rather than the settings so that a plugin's own process,
    /// which reads no settings at all, hears the same answer by inheriting it.
    /// </remarks>
    private static bool Wanted =>
        Environment.GetEnvironmentVariable(Variable) == "1";

    /// <summary>
    /// Says what the settings hold, for everything after this and for every process started from
    /// here.
    /// </summary>
    /// <remarks>
    /// Called once at startup, before anything makes a thread that will ask. Written into the
    /// environment rather than kept in a field because the other half that needs the answer is in
    /// another process.
    /// </remarks>
    /// <param name="wanted">Whether it is asked for.</param>
    public static void Wants(bool wanted) =>
        Environment.SetEnvironmentVariable(Variable, wanted ? "1" : "0");

    /// <summary>The scheduler that runs a thread until it gives way, rather than in its turn.</summary>
    private const int SchedFifo = 1;

    /// <summary>
    /// Where in that scheduler to sit.
    /// </summary>
    /// <remarks>
    /// Low on purpose. The whole win is being on this scheduler at all, since the lowest place
    /// in it is still ahead of everything not in it; and the sound server this feeds is in here
    /// too, considerably higher up, which is the right way round.
    /// </remarks>
    private const int Priority = 5;

    /// <summary>The one field of the scheduling parameters, and the only one this policy reads.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SchedParam
    {
        /// <summary>Where in the policy the thread sits.</summary>
        public int SchedPriority;
    }

    /// <summary>The calling thread, as the threads library knows it.</summary>
    [LibraryImport("libc", EntryPoint = "pthread_self")]
    private static partial IntPtr Self();

    /// <summary>Sets a thread's policy and its place in it.</summary>
    [LibraryImport("libc", EntryPoint = "pthread_setschedparam")]
    private static partial int SetSchedule(IntPtr thread, int policy, ref SchedParam param);

    /// <summary>Reads back what a thread is really scheduled as.</summary>
    [LibraryImport("libc", EntryPoint = "pthread_getschedparam")]
    private static partial int GetSchedule(IntPtr thread, out int policy, out SchedParam param);

    /// <inheritdoc/>
    public bool Take()
    {
        if (!Wanted) return false;

        if (!OperatingSystem.IsLinux()) return TakeElsewhere();

        try
        {
            var param = new SchedParam { SchedPriority = Priority };

            return SetSchedule(Self(), SchedFifo, ref param) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool PossibleOn(bool linux) => linux;

    /// <inheritdoc/>
    public bool Possible => PossibleOn(OperatingSystem.IsLinux());

    /// <summary>
    /// The same ask on the platforms this one cannot make it on.
    /// </summary>
    /// <remarks>
    /// Windows has its own answer for this, which is to say what the thread is for and let the
    /// system schedule it accordingly, and it is not written here yet. Saying so is better than
    /// a method that quietly answers false and reads as the system having refused.
    /// </remarks>
    private static bool TakeElsewhere() => false;

    /// <inheritdoc/>
    public string Said()
    {
        if (!OperatingSystem.IsLinux()) return "the ordinary scheduler";

        try
        {
            if (GetSchedule(Self(), out int policy, out var param) != 0) return "unknown";

            return policy == SchedFifo
                ? "real time, priority " + param.SchedPriority
                : "the ordinary scheduler";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }
}
