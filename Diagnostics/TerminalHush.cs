using System;
using System.Runtime.InteropServices;
using JingleBox2.Diagnostics.Interfaces;

namespace JingleBox2.Diagnostics;

/// <inheritdoc/>
/// <remarks>
/// By pointing the stream itself somewhere else, which is the only thing that reaches a library
/// writing to it directly: it holds no handle of ours and asks nothing before it writes.
///
/// Unix only. Nothing on Windows opens a device expected to refuse, and the layers that complain
/// here are not there.
/// </remarks>
public sealed partial class TerminalHush : ITerminalHush
{
    /// <summary>The stream complaints go to.</summary>
    private const int Complaints = 2;

    /// <summary>Write only, which is all that is wanted of a place to throw things.</summary>
    private const int ForWriting = 1;

    /// <summary>Where they go instead.</summary>
    private const string Nowhere = "/dev/null";

    /// <summary>Opens a file and answers its number.</summary>
    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8,
                   SetLastError = true)]
    private static partial int Open(string path, int flags);

    /// <summary>Makes one number mean what another does.</summary>
    [LibraryImport("libc", EntryPoint = "dup2", SetLastError = true)]
    private static partial int Point(int from, int to);

    /// <summary>Copies a number, so what it meant can be put back.</summary>
    [LibraryImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static partial int Copy(int number);

    /// <summary>Lets one go.</summary>
    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Let(int number);

    /// <inheritdoc/>
    public IDisposable Hushed()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return new Nothing();

        try
        {
            int kept = Copy(Complaints);

            if (kept < 0) return new Nothing();

            int nowhere = Open(Nowhere, ForWriting);

            if (nowhere < 0)
            {
                Let(kept);

                return new Nothing();
            }

            Point(nowhere, Complaints);
            Let(nowhere);

            return new Quiet(kept);
        }
        catch (Exception)
        {
            return new Nothing();
        }
    }

    /// <summary>What is given back where there was nothing to do.</summary>
    private sealed class Nothing : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }

    /// <summary>The stream pointed away, holding what it used to mean.</summary>
    /// <remarks>
    /// Putting it back is done once however this is let go of, and anything that goes wrong on
    /// the way is swallowed: a terminal that stays quiet is a smaller fault than a start that
    /// fails over having tried to be tidy.
    /// </remarks>
    private sealed class Quiet : IDisposable
    {
        /// <summary>What the stream used to mean.</summary>
        private int _kept;

        /// <summary>Takes the copy to put back.</summary>
        /// <param name="kept">The number holding what the stream was.</param>
        public Quiet(int kept) => _kept = kept;

        /// <inheritdoc/>
        public void Dispose()
        {
            int kept = _kept;

            _kept = -1;

            if (kept < 0) return;

            try
            {
                Point(kept, Complaints);
                Let(kept);
            }
            catch (Exception)
            {
            }
        }
    }
}
