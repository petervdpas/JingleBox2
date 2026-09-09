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

    /// <summary>
    /// The switch that turns the Windows half off, which is a different mechanism and so a
    /// different switch.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Variable"/>, deliberately, and the two are not two spellings of one thing.
    /// That one guards the real-time scheduler, which runs a thread ahead of everything on the
    /// machine and can starve the sound server it is feeding, so it is off until somebody asks.
    /// This guards the multimedia class scheduler, which is what every audio program on Windows
    /// uses, hands back a share rather than the machine, and is capped by the system itself. One
    /// switch over both would mean either shipping the dangerous one on or leaving the ordinary
    /// one off, and neither is what anybody wants.
    ///
    /// On unless it is turned off, which is the other way round from <see cref="Variable"/> and
    /// is the whole of why: what was there before was nothing at all.
    /// </remarks>
    public const string WindowsVariable = "JB_MMCSS";

    /// <summary>Whether the Windows half is wanted, which is yes unless it is refused outright.</summary>
    private static bool WantedOnWindows =>
        Environment.GetEnvironmentVariable(WindowsVariable) != "0";

    /// <summary>What Windows calls the class an audio thread belongs in.</summary>
    private const string ProAudio = "Pro Audio";

    /// <summary>
    /// Puts the calling thread in one of the system's multimedia classes. Nought means refused.
    /// </summary>
    /// <remarks>
    /// The number handed in is the system's own index into the class, which it fills in and
    /// which is passed back unchanged on a later call. Nought going in is what a caller with no
    /// previous answer uses.
    /// </remarks>
    [LibraryImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW",
                   StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint JoinClass(string task, ref uint index);

    /// <summary>
    /// What the system handed back, kept so the arrangement lasts as long as this does.
    /// </summary>
    /// <remarks>
    /// Windows takes the arrangement away again when this handle is closed, so it has to outlive
    /// the ask. It is never given back on purpose: the object is made on the thread it is about
    /// and lives as long as that thread's own loop, and a thread that is ending is a thread whose
    /// scheduling nobody cares about any more.
    /// </remarks>
    private nint _task;

    /// <summary>
    /// The same ask on Windows, which has its own mechanism for it and had none of this written.
    /// </summary>
    /// <remarks>
    /// The real-time scheduler above is a Linux idea and there is no such thing here. What
    /// Windows has instead is a class a thread says it belongs to, and the system then guarantees
    /// that class a share of every interval rather than putting it in front of everything. Pro
    /// Audio is the one meant for exactly this, and it is what every audio program on this
    /// platform asks for.
    ///
    /// Nothing is thrown, including the library not being there: an answer of no is ordinary and
    /// the program is correct without it.
    /// </remarks>
    private bool TakeOnWindows()
    {
        if (!WantedOnWindows) return false;

        try
        {
            uint index = 0;

            _task = JoinClass(ProAudio, ref index);

            return _task != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Windows is answered first and on its own terms. It is not asking for the same thing, so it
    /// is not behind the same switch: see <see cref="WindowsVariable"/>.
    /// </remarks>
    public bool Take()
    {
        if (OperatingSystem.IsWindows()) return TakeOnWindows();

        if (!Wanted) return false;

        if (!OperatingSystem.IsLinux()) return false;

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

    /// <inheritdoc/>
    /// <remarks>
    /// Windows is what this holds rather than what the system says, since there is nothing to ask
    /// it: the class a thread is in is not readable back, only the handle that keeps it.
    /// </remarks>
    public string Said()
    {
        if (OperatingSystem.IsWindows())
            return _task != 0 ? "the " + ProAudio + " class" : "the ordinary scheduler";

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
