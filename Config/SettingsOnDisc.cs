using System;
using System.Diagnostics;
using System.Timers;
using JingleBox2.Config.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Config;

/// <inheritdoc/>
public sealed class SettingsOnDisc : ISettingsOnDisc
{
    /// <summary>How long the hints have to stop before the file is written.</summary>
    /// <remarks>
    /// A level dragged across a strip is one thing somebody did and several hundred hints, so the
    /// file is written when the hand stops rather than while it moves. Under half a second, which
    /// is short enough that letting go and closing the application is not a race.
    /// </remarks>
    private static readonly TimeSpan Settles = TimeSpan.FromMilliseconds(400);

    /// <summary>And how long a look is put off for while nothing is being said.</summary>
    /// <remarks>
    /// **This is the net rather than the writing**, so it is as slow as it can be without
    /// somebody losing work: five seconds is the most that can be between a setting nobody hinted
    /// at and it being on the disc. It is also what bounds a gesture that never stops, since a
    /// hand on a fader for a minute would otherwise hold the file back for a minute.
    ///
    /// What a look costs where nothing has moved is one serialising of a document a few
    /// kilobytes long, which is the same work the writing would have done anyway, and no disc at
    /// all. Slow on purpose all the same, since this application is meant to run on machines with
    /// nothing to spare.
    /// </remarks>
    private static readonly TimeSpan Often = TimeSpan.FromSeconds(5);

    /// <summary>How often the two above are compared against, which is the clock's own rate.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(200);

    /// <summary>How the settings are turned into a file, and written whole.</summary>
    private readonly IConfigStore _store;

    /// <summary>The block being followed.</summary>
    private readonly ISettingsBlock _settings;

    /// <summary>The clock, which runs for as long as this does.</summary>
    /// <remarks>
    /// Not the drawing thread's. Writing the settings is a serialising and a file, and neither
    /// belongs in front of a window that is being drawn.
    /// </remarks>
    private readonly Timer _clock;

    /// <summary>What was last written, so a look is a comparison rather than a write.</summary>
    /// <remarks>
    /// Nothing to begin with, which reads as unknown and makes the first look write: the file on
    /// the disc was written by a previous run and this has no way of knowing whether the document
    /// it is holding still says the same thing, since it is handed the document rather than the
    /// reading of it.
    /// </remarks>
    private string? _wrote;

    /// <summary>How long since something said it had moved, or nothing since the last look.</summary>
    private Stopwatch? _hinted;

    /// <summary>And how long since the last look, which is what the net is measured against.</summary>
    private readonly Stopwatch _looked = Stopwatch.StartNew();

    /// <summary>One look at a time, since the clock and the way out can both ask.</summary>
    private readonly object _looking = new();

    /// <summary>Follows a settings block, and keeps the file saying what it says.</summary>
    /// <param name="store">How the settings are turned into a file.</param>
    /// <param name="settings">The block to follow.</param>
    public SettingsOnDisc(IConfigStore store, ISettingsBlock settings)
    {
        _store = store;
        _settings = settings;

        _settings.Changed += Hinted;

        _clock = new Timer(Tick.TotalMilliseconds) { AutoReset = true };
        _clock.Elapsed += (_, _) => Due();
        _clock.Start();
    }

    /// <summary>Something moved, which is a reason to look sooner rather than a reason to write.</summary>
    private void Hinted() => _hinted = Stopwatch.StartNew();

    /// <summary>
    /// Whether this tick is one to look on: the hints have stopped, or the net has come round.
    /// </summary>
    private void Due()
    {
        bool quiet = _hinted is { } since && since.Elapsed >= Settles;

        if (!quiet && _looked.Elapsed < Often) return;

        Check();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// **Anything thrown costs one look.** What is being read is the document the rest of the
    /// application is working in, from a thread that is not the one working in it, so a list
    /// added to at the moment it is walked can refuse to be serialised. The window is a
    /// fraction of a millisecond and the answer is to come back in a moment, which is what the
    /// clock does anyway; what may not happen is the application going down from a thread nobody
    /// is watching, over a settings file.
    ///
    /// The hint is cleared before the work rather than after it, so a change arriving while this
    /// is serialising is a hint that stands rather than one that has just been answered.
    /// </remarks>
    public bool Check()
    {
        lock (_looking)
        {
            _hinted = null;
            _looked.Restart();

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

    /// <inheritdoc/>
    /// <remarks>
    /// **One last look on the way out**, since the way out is the one moment that cannot be
    /// waited through: whatever was changed in the last fraction of a second is otherwise the
    /// thing somebody comes back to find missing.
    /// </remarks>
    public void Dispose()
    {
        _settings.Changed -= Hinted;

        _clock.Stop();
        _clock.Dispose();

        Check();
    }
}
