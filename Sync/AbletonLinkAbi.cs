using System.Runtime.InteropServices;

namespace JingleBox2.Sync;

/// <summary>
/// Ableton Link's C wrapper, <c>abl_link</c>, as this application calls it.
/// </summary>
/// <remarks>
/// Declarations and nothing else, which is why it is static: an ABI is data with a compiler
/// attached, the rule <c>Vst3Abi</c> and <c>ClapAbi</c> already keep. What the calls mean, and
/// which thread may make them, is <see cref="Interfaces.IAbletonLink"/>'s.
///
/// The library is built from Link's own source by <c>native/abletonlink/build.sh</c>, at the
/// release pinned there, and carried beside BASS: <c>libabl_link.so</c> on Linux and
/// <c>abl_link.dll</c> on Windows, both of which the runtime finds under the one name here.
///
/// **Both handles are a struct holding one pointer, passed and returned by value**, and they are
/// declared here as that pointer. A struct of exactly one pointer travels in the same register as
/// the pointer on every calling convention this application runs on: System V on x86-64 and
/// aarch64, and the Windows x64 one. A second field in either struct would break that, so the
/// pinned release is also a pin on the layout.
///
/// A C <c>bool</c> is one byte, and a marshalled one is four unless it is told otherwise, which
/// is what every <c>U1</c> here is for.
/// </remarks>
internal static partial class AbletonLinkAbi
{
    /// <summary>The library's name as the runtime looks it up on every platform.</summary>
    public const string Library = "abl_link";

    /// <summary>A Link instance with a starting tempo. Disabled until enabled.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_create")]
    public static partial nint Create(double bpm);

    /// <summary>Takes an instance down.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_destroy")]
    public static partial void Destroy(nint link);

    /// <summary>Joins or leaves the network.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_enable")]
    public static partial void Enable(nint link, [MarshalAs(UnmanagedType.U1)] bool enable);

    /// <summary>Whether it is on the network.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_is_enabled")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool IsEnabled(nint link);

    /// <summary>Whether start and stop are shared with the other peers.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_enable_start_stop_sync")]
    public static partial void EnableStartStopSync(nint link, [MarshalAs(UnmanagedType.U1)] bool enable);

    /// <summary>How many other peers are in the session.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_num_peers")]
    public static partial ulong NumPeers(nint link);

    /// <summary>Told when the number of peers moves, on Link's own thread.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_set_num_peers_callback")]
    public static unsafe partial void SetNumPeersCallback(
        nint link, delegate* unmanaged[Cdecl]<ulong, nint, void> callback, nint context);

    /// <summary>Told when the session's tempo moves, on Link's own thread.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_set_tempo_callback")]
    public static unsafe partial void SetTempoCallback(
        nint link, delegate* unmanaged[Cdecl]<double, nint, void> callback, nint context);

    /// <summary>Told when the session starts or stops, on Link's own thread.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_set_start_stop_callback")]
    public static unsafe partial void SetStartStopCallback(
        nint link, delegate* unmanaged[Cdecl]<byte, nint, void> callback, nint context);

    /// <summary>Link's clock, in microseconds.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_clock_micros")]
    public static partial long ClockMicros(nint link);

    /// <summary>A session state to capture into and commit from.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_create_session_state")]
    public static partial nint CreateSessionState();

    /// <summary>Takes a session state down.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_destroy_session_state")]
    public static partial void DestroySessionState(nint state);

    /// <summary>Reads the session into a state, from any thread but the audio one.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_capture_app_session_state")]
    public static partial void CaptureAppSessionState(nint link, nint state);

    /// <summary>Hands a changed state to the session, from any thread but the audio one.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_commit_app_session_state")]
    public static partial void CommitAppSessionState(nint link, nint state);

    /// <summary>The state's tempo.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_tempo")]
    public static partial double Tempo(nint state);

    /// <summary>Sets the state's tempo, taking effect at a moment.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_set_tempo")]
    public static partial void SetTempo(nint state, double bpm, long atMicros);

    /// <summary>The beat at a moment, against a quantum.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_beat_at_time")]
    public static partial double BeatAtTime(nint state, long micros, double quantum);

    /// <summary>The moment of a beat, against a quantum.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_time_at_beat")]
    public static partial long TimeAtBeat(nint state, double beat, double quantum);

    /// <summary>Whether the state says the transport is playing.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_is_playing")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool IsPlaying(nint state);

    /// <summary>Sets whether the transport is playing, taking effect at a moment.</summary>
    [LibraryImport(Library, EntryPoint = "abl_link_set_is_playing")]
    public static partial void SetIsPlaying(nint state, [MarshalAs(UnmanagedType.U1)] bool playing, long atMicros);
}
