using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins;

namespace JingleBox2.Diagnostics.Interfaces;

/// <summary>
/// The note a run leaves saying it is under way, and what a note still lying there means.
/// </summary>
/// <remarks>
/// A managed exception nobody caught can be written down as it happens. A plugin dereferencing
/// a null pointer inside this process cannot: the process is gone, and code that runs
/// afterwards does not exist. So that kind of ending is caught the only way it can be, by
/// leaving a note on the way in and looking for it on the way back up, and a note still lying
/// there next time means the run that wrote it never finished.
///
/// The two directions are one contract on purpose. What is written and what is read back are a
/// pair, and a pair split between two methods on a class nobody can build in a test is a pair
/// that drifts: the marker says when the run began, and a reader that stopped understanding
/// that would date a crash by the moment it was found out, which is the next run starting up.
/// </remarks>
public interface IRunMarker
{
    /// <summary>What to write into the marker for a run beginning now.</summary>
    /// <param name="started">When the run began.</param>
    /// <param name="version">Which build it is, since a report from a version nobody can name says little.</param>
    string Compose(DateTime started, string version);

    /// <summary>
    /// When the run that left this marker began, or nothing when the marker does not say.
    /// </summary>
    /// <remarks>
    /// Nothing rather than a guess. A report with no start time says it does not know, which is
    /// true; one dated by the moment the marker was found says the crash happened at startup,
    /// which is a lie that sends somebody looking in the wrong place.
    /// </remarks>
    /// <param name="lines">The marker as it was read back, line by line.</param>
    DateTime? StartedFrom(IEnumerable<string> lines);

    /// <summary>
    /// The plugin crashes that belong to the run that stopped, rather than to any run before it.
    /// </summary>
    /// <remarks>
    /// The blocked list is kept across runs, so without the time it was written down a report
    /// would name every plugin that has ever fallen over rather than the one that just did.
    /// </remarks>
    /// <param name="blocked">Every plugin that has been shut out, over every run.</param>
    /// <param name="since">When the run being reported on began.</param>
    IReadOnlyList<PluginCrash> Since(IReadOnlyList<PluginCrash> blocked, DateTime since);
}
