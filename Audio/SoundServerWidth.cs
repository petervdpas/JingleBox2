using System;
using System.Runtime.InteropServices;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// PipeWire, through the environment its ALSA layer reads. Nothing is asked of the server and
/// nothing is opened: this is a string put where the layer will look, and everything that follows
/// reads it or does not.
/// </remarks>
public sealed partial class SoundServerWidth : ISoundServerWidth
{
    /// <summary>
    /// Puts a variable where a library written in C will find it.
    /// </summary>
    /// <remarks>
    /// **The runtime's own way of setting one does not reach a native library, and nothing says
    /// so.** On this platform it keeps its own table of the environment and writes into that;
    /// what a library loaded into this process reads is the real one, which is untouched. So the
    /// variable was set, could be read back by the runtime, and was simply not there as far as
    /// the sound server's ALSA layer was concerned. Measured rather than reasoned about: set
    /// through the runtime and read through <c>getenv</c>, the answer is nothing at all.
    ///
    /// The last argument is whether to write over one that is already there, and it is nought
    /// here, which is the same promise this class makes anyway.
    /// </remarks>
    /// <param name="name">The variable.</param>
    /// <param name="value">What it should say.</param>
    /// <param name="overwrite">Nought to leave one that is already there alone.</param>
    /// <returns>Nought where it was set.</returns>
    [LibraryImport("libc", EntryPoint = "setenv", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Put(string name, string value, int overwrite);

    /// <summary>What the server's ALSA layer reads its properties out of.</summary>
    private const string Variable = "PIPEWIRE_ALSA";

    /// <summary>Two channels, said the way that layer spells it.</summary>
    /// <remarks>
    /// The width alone. Everything else about the stream, the rate, the format and how much is
    /// held, is the library's to choose and is chosen well; saying more here would be this
    /// application deciding things it has no view on.
    /// </remarks>
    private const string Stereo = "{ alsa.channels=2 }";

    /// <summary>Whether this machine has that server at all.</summary>
    private readonly bool _linux;

    /// <summary>What was arranged, kept for the log.</summary>
    private bool _asked;

    /// <summary>Whether somebody had already said something of their own.</summary>
    private bool _theirs;

    /// <summary>Takes the platform.</summary>
    /// <param name="linux">True on Linux. Left out, this machine's own answer.</param>
    public SoundServerWidth(bool? linux = null) => _linux = linux ?? OperatingSystem.IsLinux();

    /// <inheritdoc/>
    public bool Ask()
    {
        if (!_linux) return false;

        try
        {
            _theirs = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable))
                      || !string.IsNullOrWhiteSpace(Read());

            if (_theirs) return false;

            Environment.SetEnvironmentVariable(Variable, Stereo);

            _asked = Put(Variable, Stereo, 0) == 0;

            return _asked;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>What the real environment holds, which is the one a native library reads.</summary>
    /// <remarks>
    /// Asked of the same place it is written to, since the runtime's own table and the process's
    /// are two different things and this class is about the second.
    /// </remarks>
    private static string? Read()
    {
        try
        {
            var held = Get(Variable);

            return held == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(held);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Reads one out of the real environment.</summary>
    /// <param name="name">The variable.</param>
    /// <returns>What it says, or nothing where it is not set.</returns>
    [LibraryImport("libc", EntryPoint = "getenv", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr Get(string name);

    /// <inheritdoc/>
    public string Said()
    {
        if (!_linux) return "whatever the sound library asks its device for";
        if (_theirs) return "what this machine was already told to give, which is not ours to change";

        return _asked
            ? "two channels, which is what the sound server's own layer was told to offer"
            : "whatever the sound library asks its device for, since the server could not be told";
    }
}
