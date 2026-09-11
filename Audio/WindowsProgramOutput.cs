using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through <c>Windows.Media.Internal.AudioPolicyConfig</c>, which is the interface behind the
/// per-program output in Windows' own Volume mixer. It is undocumented, and it has shipped in
/// EarTrumpet and SoundSwitch for years: the shape below is theirs, read off their sources, and
/// the credit is theirs. Nothing was copied.
///
/// **Undocumented means it can be gone tomorrow**, so every call is guarded and every failure is
/// an answer rather than an exception: what is lost when it goes is a switch that stops working,
/// not an application that will not run.
///
/// **It does not work on this runtime, and what stops it is not Windows.** Measured on .NET 10,
/// Windows 11 26200:
///
/// - <c>[MarshalAs(UnmanagedType.HString)]</c> on the class id throws
///   <c>MarshalDirectiveException: Cannot marshal &apos;parameter #1&apos;</c>. Built-in WinRT
///   marshalling came out of the runtime in .NET 5 and this is what is left of it. **This is the
///   one that fires**, on every start, so the switch has never worked here.
/// - <c>ComInterfaceType.InterfaceIsIInspectable</c> throws
///   <c>PlatformNotSupportedException</c> for the same reason, which is what is waiting once the
///   first is fixed.
///
/// The interface itself is present and activates cleanly: building the class id by hand with
/// <see cref="WindowsCreateString"/> and asking for it answers <c>hr = 0</c> and a real object.
/// So the sentence this used to log, that the policy is not on this machine, was wrong and sent
/// two people looking at the operating system for an hour.
///
/// **What is not known is the slot.** The way out is raw calls through the vtable, and that needs
/// the offset of <see cref="IAudioPolicyConfig.SetPersistedDefaultAudioEndpoint"/> to be right,
/// which is not a compile error and is a crash. The nineteen stubs below have never been checked
/// against anything: probing found slot 24 answering <c>S_OK</c> for both roles and persisting
/// nothing at all in
/// <c>HKCU\Software\Microsoft\Internet Explorer\LowRegistry\Audio\PolicyConfig\PropertyStore</c>,
/// which is where Windows keeps these, so **that slot is some other function that accepts the
/// same arguments**. Slots 25 and 26 answer <c>E_INVALIDARG</c>.
///
/// **So the layout has to come from EarTrumpet or SoundSwitch rather than from a probe**, which
/// is where the shape below came from in the first place. A number that answers <c>S_OK</c> and
/// does nothing is worse than one that fails, and guessing at it on somebody&apos;s machine is
/// calling an unknown function on an undocumented object.
///
/// Until then there is a route that needs none of this and works today, and it is in the help
/// under SETTINGS, Recording input: a spare output or a virtual audio cable as where the source
/// is sent, with Windows&apos; own Volume mixer doing the moving.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsProgramOutput : IProgramOutput
{
    /// <summary>The activatable class the factory comes out of.</summary>
    private const string PolicyConfig = "Windows.Media.Internal.AudioPolicyConfig";

    /// <summary>Which direction is meant. Nought is what the system calls playing out.</summary>
    /// <remarks>
    /// The system's own <c>EDataFlow</c>, where playing is nought, recording is one and both is
    /// two. It said two here, which is the value for both and is not what this asks for; the
    /// number was right and the sentence beside it was wrong, which next to a call placed by
    /// slot is exactly the kind of thing that gets read instead of the code.
    /// </remarks>
    private const int Render = 0;

    /// <summary>What the output is for. Both are set, since a program may ask under either.</summary>
    /// <remarks>
    /// A program picks the role it opens with, and the two that matter are the ordinary one and
    /// the one anything media-shaped uses. Setting one and not the other leaves a program whose
    /// choice of role decides whether this worked, which is a switch that works for some
    /// programs and not others with nothing to say why.
    /// </remarks>
    private const int Console = 0;

    /// <inheritdoc cref="Console"/>
    private const int Multimedia = 1;

    /// <summary>Turns an endpoint id into the path the call takes. Holds nothing.</summary>
    private readonly IMmDeviceToken _token = new MmDeviceToken();

    /// <summary>The factory, once it has been asked for, or nothing where it would not come.</summary>
    private IAudioPolicyConfig? _policy;

    /// <summary>Whether asking has already been tried and failed, so it is not tried per press.</summary>
    private bool _refused;

    /// <inheritdoc/>
    public bool CanPoint => Policy() != null;

    /// <inheritdoc/>
    public bool Point(int processId, string endpoint)
    {
        if (processId <= 0 || string.IsNullOrWhiteSpace(endpoint)) return false;

        return Say(processId, _token.Wrap(endpoint));
    }

    /// <inheritdoc/>
    public bool Release(int processId) => processId > 0 && Say(processId, "");

    /// <summary>
    /// Tells the system where that program plays, under both roles.
    /// </summary>
    /// <remarks>
    /// The string is one the system takes ownership of reading and we own creating, so it is
    /// let go of whatever happened: a handle leaked per press is a handle leaked for the life of
    /// a show. An empty path is handed over as nothing at all, which is what says "no preference
    /// of ours" rather than "play nowhere".
    /// </remarks>
    /// <param name="processId">Which program.</param>
    /// <param name="path">The device interface path, or empty to give the choice back.</param>
    private bool Say(int processId, string path)
    {
        var policy = Policy();

        if (policy == null) return false;

        IntPtr text = IntPtr.Zero;

        try
        {
            if (path.Length > 0 && WindowsCreateString(path, (uint)path.Length, out text) != 0)
            {
                Log.Write(LogArea.Audio, () => "outputs: the endpoint could not be named");

                return false;
            }

            int console = policy.SetPersistedDefaultAudioEndpoint((uint)processId, Render, Console, text);
            int media = policy.SetPersistedDefaultAudioEndpoint((uint)processId, Render, Multimedia, text);

            bool said = console >= 0 && media >= 0;

            if (!said)
                Log.Write(LogArea.Audio, () =>
                    "outputs: " + processId + " would not be pointed: 0x" + console.ToString("X8") + " 0x" + media.ToString("X8"));

            return said;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Audio, () => "outputs: " + processId + " would not be pointed: " + bad.Message);

            return false;
        }
        finally
        {
            if (text != IntPtr.Zero) WindowsDeleteString(text);
        }
    }

    /// <summary>The factory, asked for once.</summary>
    /// <remarks>
    /// Once because it is a COM activation of an undocumented class: a machine where it is not
    /// there is a machine where it will not be there in a moment either, and asking per press
    /// would be a thrown exception per press.
    /// </remarks>
    private IAudioPolicyConfig? Policy()
    {
        if (_policy != null || _refused) return _policy;

        _refused = true;

        try
        {
            var iid = typeof(IAudioPolicyConfig).GUID;

            int hr = RoGetActivationFactory(PolicyConfig, ref iid, out object factory);

            if (hr != 0)
            {
                Log.Write(LogArea.Audio, () =>
                    "outputs: the policy would not activate: 0x" + hr.ToString("X8"));

                return null;
            }

            _policy = factory as IAudioPolicyConfig;
            _refused = _policy == null;

            if (_policy == null)
                Log.Write(LogArea.Audio, () =>
                    "outputs: the policy activated and would not be read as " + nameof(IAudioPolicyConfig));

            return _policy;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Audio, () =>
                "outputs: the policy could not be reached from this build: " + bad.Message
                + " (the interface is on the machine; this is what stops us using it)");

            return null;
        }
    }

    /// <summary>
    /// The one call this application makes on the policy, with everything before it stubbed.
    /// </summary>
    /// <remarks>
    /// The members above the one that is wanted are there because a COM interface is an ordered
    /// list of slots: leaving them out would put the call at the wrong offset, which is not a
    /// compile error and is a crash. They are named for what they are, which is unimplemented.
    /// </remarks>
    [Guid("ab3d4648-e242-459f-b02f-541c70306324")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    private interface IAudioPolicyConfig
    {
        /// <summary>Slot, not implemented here.</summary>
        int NotUsed01();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed02();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed03();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed04();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed05();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed06();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed07();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed08();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed09();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed10();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed11();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed12();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed13();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed14();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed15();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed16();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed17();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed18();
        /// <inheritdoc cref="NotUsed01"/>
        int NotUsed19();

        /// <summary>Says where one program plays, and answers rather than throwing.</summary>
        /// <param name="processId">Which program.</param>
        /// <param name="flow">Playing out or recording.</param>
        /// <param name="role">What the output is for.</param>
        /// <param name="deviceId">The device interface path as a string handle, or nothing.</param>
        [PreserveSig]
        int SetPersistedDefaultAudioEndpoint(uint processId, int flow, int role, IntPtr deviceId);
    }

    /// <summary>Asks the system for the factory of an activatable class.</summary>
    /// <param name="activatableClassId">Which class.</param>
    /// <param name="iid">Which interface on it.</param>
    /// <param name="factory">What came back.</param>
    [DllImport("combase.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        [MarshalAs(UnmanagedType.HString)] string activatableClassId,
        [In] ref Guid iid,
        [Out, MarshalAs(UnmanagedType.IUnknown)] out object factory);

    /// <summary>Makes a string the system can read, which has to be let go of again.</summary>
    /// <param name="text">The characters.</param>
    /// <param name="length">How many of them.</param>
    /// <param name="handle">What was made.</param>
    [DllImport("combase.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string text, uint length, out IntPtr handle);

    /// <summary>Lets one of those go.</summary>
    /// <param name="handle">What was made.</param>
    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr handle);
}
