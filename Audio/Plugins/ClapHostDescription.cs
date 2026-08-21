using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// What the host tells a plugin about itself, and the four calls a plugin can make back.
/// </summary>
/// <remarks>
/// The struct is unmanaged and lives as long as the plugin does, because the plugin keeps the
/// pointer. Extensions are answered with null: this host provides none yet, which is a legal
/// answer to every one of them and is what keeps a first plugin working without implementing
/// half the specification.
/// </remarks>
internal static unsafe class ClapHostDescription
{
    private const string HostName = "JingleBox2";
    private const string HostVendor = "JingleBox2";
    private const string HostUrl = "https://github.com/";
    private const string HostVersion = "1.0";

    private static readonly nint Name = Marshal.StringToCoTaskMemUTF8(HostName);
    private static readonly nint Vendor = Marshal.StringToCoTaskMemUTF8(HostVendor);
    private static readonly nint Url = Marshal.StringToCoTaskMemUTF8(HostUrl);
    private static readonly nint Version = Marshal.StringToCoTaskMemUTF8(HostVersion);

    /// <summary>The CLAP version this host was written against.</summary>
    private static readonly ClapVersion Abi = new() { Major = 1, Minor = 1, Revision = 7 };

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

        return host;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void* GetExtension(ClapHost* host, byte* id) => null;

    /// <summary>
    /// A plugin asking to be restarted, to be scheduled, or for a call on the main thread.
    /// All three are answered by doing nothing, which is safe: the host runs it every block
    /// regardless, and nothing here defers work to the main thread yet.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void RequestRestart(ClapHost* host) { }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void RequestProcess(ClapHost* host) { }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void RequestCallback(ClapHost* host) { }
}
