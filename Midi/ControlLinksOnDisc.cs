using System;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Hints.Interfaces;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class ControlLinksOnDisc : IControlLinksOnDisc
{
    /// <summary>How long the links have to stop moving before the file is written.</summary>
    /// <remarks>
    /// The same four hundred milliseconds the settings settle for, and for a sharper reason: a
    /// hand sweeping a knob that has just been learned sends a hundred messages, and the first
    /// few of them teach the router what kind of control it is, which writes onto the link.
    /// </remarks>
    private static readonly TimeSpan Settles = TimeSpan.FromMilliseconds(400);

    /// <summary>And how long a look may be put off in all.</summary>
    private static readonly TimeSpan Often = TimeSpan.FromSeconds(5);

    /// <summary>How the links are turned into a file, and written whole.</summary>
    private readonly IRemoteControlLinks _store;

    /// <summary>The block being followed.</summary>
    private readonly IControlLinkBlock _block;

    /// <summary>What was last written, so a look is a comparison rather than a write.</summary>
    private string? _wrote;

    /// <summary>One look at a time, since the clock and the way out can both ask.</summary>
    private readonly object _looking = new();

    /// <summary>Follows a links block, and keeps the file saying what it says.</summary>
    /// <remarks>
    /// **Holds no clock of its own**, the same as the settings' writer: being told and looking
    /// anyway are one module, so a hint here runs at the same rate, on the same thread and under
    /// the same rules as the one under a pad's chain.
    /// </remarks>
    /// <param name="store">How the links are turned into a file.</param>
    /// <param name="block">The block to follow.</param>
    /// <param name="hints">The one clock every hint runs on.</param>
    public ControlLinksOnDisc(IRemoteControlLinks store, IControlLinkBlock block, IHintClock hints)
    {
        _store = store;
        _block = block;

        var said = hints.Gathered("the links file", Settles, Often, () => Check());

        hints.Often("the links file, unasked", Often, () => Check());

        _block.Changed += said.Moved;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// **Anything thrown costs one look.** The list is edited from the drawing thread and from
    /// the MIDI thread and read here on a third, so one added to at the moment it is walked can
    /// refuse to be serialised. The answer is to come back in a moment, which is what the clock
    /// does anyway; what may not happen is the application going down from a thread nobody is
    /// watching, over a file of knob assignments.
    /// </remarks>
    public bool Check()
    {
        lock (_looking)
        {
            try
            {
                string written = _store.Written(_block.Links);

                if (written == _wrote) return false;

                _store.Keep(written);

                _wrote = written;

                return true;
            }
            catch (Exception bad)
            {
                Log.Fault(LogArea.Midi, "the links could not be written", bad);

                return false;
            }
        }
    }
}
