using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// What the host tells a plugin about itself, and the four calls a plugin can make back.
/// </summary>
/// <remarks>
/// The struct is unmanaged and lives as long as the plugin does, because the plugin keeps the
/// pointer. Most extensions are answered with null, which is a legal answer to every one of
/// them. The three that are not are the ones a plugin needs before it can draw anything: a
/// clock, a way to have its X11 connection watched, and somewhere to ask for a different size.
/// See <see cref="ClapHostExtensions"/>.
/// </remarks>
internal static unsafe class ClapHostDescription
{
    /// <summary>What this host calls itself. Plenty of plugins print it in their own window.</summary>
    private const string HostName = "JingleBox2";

    /// <summary>Who wrote the host. The same name, since there is only the one.</summary>
    private const string HostVendor = "JingleBox2";

    /// <summary>Where a plugin would send somebody who wanted to know about the host.</summary>
    private const string HostUrl = "https://github.com/";

    /// <summary>The host's own version, as a plugin reads it.</summary>
    private const string HostVersion = "1.0";

    /// <summary>
    /// The four strings as unmanaged UTF-8, allocated once and never freed. A plugin keeps the
    /// pointers it is given for as long as it is loaded, so a marshalled string that lived only
    /// as long as the call would be a dangling pointer inside somebody else's code. Four
    /// allocations for the life of the process is the whole cost.
    /// </summary>
    private static readonly nint Name = Marshal.StringToCoTaskMemUTF8(HostName);

    /// <inheritdoc cref="Name"/>
    private static readonly nint Vendor = Marshal.StringToCoTaskMemUTF8(HostVendor);

    /// <inheritdoc cref="Name"/>
    private static readonly nint Url = Marshal.StringToCoTaskMemUTF8(HostUrl);

    /// <inheritdoc cref="Name"/>
    private static readonly nint Version = Marshal.StringToCoTaskMemUTF8(HostVersion);

    /// <summary>
    /// The CLAP version this host was written against. A plugin compares its own against it and
    /// is entitled to refuse to load, so this is a statement about what has been implemented
    /// here rather than about what is installed.
    /// </summary>
    private static readonly ClapVersion Abi = new() { Major = 1, Minor = 1, Revision = 7 };

    /// <summary>
    /// Builds the host struct a plugin is handed when it is created.
    /// </summary>
    /// <remarks>
    /// Allocated with <c>NativeMemory</c> rather than pinned managed memory because the plugin
    /// holds the pointer for its whole life and the collector must never be in a position to
    /// move it. Zeroed first, so any field added to the struct later reads as null to a plugin
    /// built against the older layout instead of reading as rubbish.
    /// </remarks>
    public static ClapHost* Create()
    {
        var host = (ClapHost*)NativeMemory.AllocZeroed(1, (nuint)sizeof(ClapHost));

        host->Version = Abi;
        host->HostData = null;
        host->Name = (byte*)Name;
        host->Vendor = (byte*)Vendor;
        host->Url = (byte*)Url;
        host->HostVersion = (byte*)Version;

        host->GetExtension = &GetExtension;
        host->RequestRestart = &RequestRestart;
        host->RequestProcess = &RequestProcess;
        host->RequestCallback = &RequestCallback;

        ClapHostExtensions.Reserve(host);

        return host;
    }

    /// <summary>
    /// A plugin asking whether the host implements something. Answered by name out of
    /// <see cref="ClapHostExtensions"/>, and null for everything else, which is a legal answer to
    /// every extension there is.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void* GetExtension(ClapHost* host, byte* id) => ClapHostExtensions.Extension(id);

    /// <summary>
    /// A plugin asking to be restarted, to be scheduled, or for a call on the main thread.
    /// All three are answered by doing nothing, which is safe: the host runs it every block
    /// regardless, and nothing here defers work to the main thread yet.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void RequestRestart(ClapHost* host) { }

    /// <inheritdoc cref="RequestRestart"/>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void RequestProcess(ClapHost* host) { }

    /// <inheritdoc cref="RequestRestart"/>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void RequestCallback(ClapHost* host) { }
}
