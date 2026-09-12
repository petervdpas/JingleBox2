using System;
using JingleBox2.Config.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Hints.Interfaces;

namespace JingleBox2.Config;

/// <inheritdoc/>
public sealed class SettingsOnDisc : ISettingsOnDisc
{
    /// <summary>How long the settings have to stop moving before the file is written.</summary>
    /// <remarks>
    /// A level dragged across a strip is one thing somebody did and several hundred hints, so the
    /// file is written when the hand stops rather than while it moves. Under half a second, which
    /// is short enough that letting go and closing the application is not a race.
    /// </remarks>
    private static readonly TimeSpan Settles = TimeSpan.FromMilliseconds(400);

    /// <summary>And how long a look may be put off in all.</summary>
    /// <remarks>
    /// Five seconds is the most that can be between a setting and it being on the disc, whether
    /// because a hand has not let go of a fader or because nobody said it had moved at all. What
    /// a look costs where nothing has moved is one serialising of a document a few kilobytes
    /// long, and no disc.
    /// </remarks>
    private static readonly TimeSpan Often = TimeSpan.FromSeconds(5);

    /// <summary>How the settings are turned into a file, and written whole.</summary>
    private readonly IConfigStore _store;

    /// <summary>The block being followed.</summary>
    private readonly ISettingsBlock _settings;

    /// <summary>What was last written, so a look is a comparison rather than a write.</summary>
    /// <remarks>
    /// Nothing to begin with, which reads as unknown and makes the first look write: the file on
    /// the disc was written by a previous run and this has no way of knowing whether the document
    /// it is holding still says the same thing, since it is handed the document rather than the
    /// reading of it.
    /// </remarks>
    private string? _wrote;

    /// <summary>One look at a time, since the clock and the way out can both ask.</summary>
    private readonly object _looking = new();

    /// <summary>Follows a settings block, and keeps the file saying what it says.</summary>
    /// <remarks>
    /// **Holds no clock of its own.** Being told and looking anyway are the two halves of every
    /// deferred write in this application, and they are one module:
    /// <see cref="IHintClock"/> drives both, so a hint here runs at the same rate, on the same
    /// thread and under the same rules as the one under a pad's chain.
    /// </remarks>
    /// <param name="store">How the settings are turned into a file.</param>
    /// <param name="settings">The block to follow.</param>
    /// <param name="hints">The one clock every hint runs on.</param>
    public SettingsOnDisc(IConfigStore store, ISettingsBlock settings, IHintClock hints)
    {
        _store = store;
        _settings = settings;

        var said = hints.Gathered("the settings file", Settles, Often, () => Check());

        hints.Often("the settings file, unasked", Often, () => Check());

        _settings.Changed += said.Moved;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// **Anything thrown costs one look.** What is being read is the document the rest of the
    /// application is working in, from a thread that is not the one working in it, so a list
    /// added to at the moment it is walked can refuse to be serialised. The window is a fraction
    /// of a millisecond and the answer is to come back in a moment, which is what the clock does
    /// anyway; what may not happen is the application going down from a thread nobody is
    /// watching, over a settings file.
    /// </remarks>
    public bool Check()
    {
        lock (_looking)
        {
            try
            {
                string written = _store.Written(_settings.Config);

                if (written == _wrote) return false;

                _store.Write(written);

                _wrote = written;

                return true;
            }
            catch (Exception bad)
            {
                Log.Fault(LogArea.App, "the settings could not be written", bad);

                return false;
            }
        }
    }
}
