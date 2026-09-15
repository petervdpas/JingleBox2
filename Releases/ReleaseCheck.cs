using System;
using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Releases.Interfaces;
using JingleBox2.Releases.Records;
using JingleBox2.UI.Enums;

namespace JingleBox2.Releases;

/// <inheritdoc/>
public sealed class ReleaseCheck(IReleaseFeed? feed = null) : IReleaseCheck
{
    /// <summary>Where the latest release is asked for.</summary>
    private readonly IReleaseFeed _feed = feed ?? new GitHubReleases();

    /// <inheritdoc/>
    /// <remarks>
    /// A feed is promised not to throw and is not trusted to keep the promise: this runs on its
    /// own as the application starts, with nobody waiting on it to catch what it throws.
    /// </remarks>
    public async Task<ReleaseNews?> Check(string running, CancellationToken cancel = default)
    {
        try
        {
            var latest = await _feed.Latest(cancel).ConfigureAwait(false);

            return latest == null ? null : Compare(running, latest);
        }
        catch (Exception e)
        {
            Log.Write(LogArea.App, () => "releases: the check failed: " + e.Message);
            return null;
        }
    }

    /// <inheritdoc/>
    public ReleaseNews? Compare(string running, Release latest)
    {
        if (Read(latest.Tag) is not { } newest) return null;

        var shown = "v" + Shown(newest);

        if (Read(running) is not { } current || current == new Version(0, 0, 0))
            return new ReleaseNews("This is a local build. The latest release is " + shown + ".",
                                   StatusKind.Plain, latest.Page);

        return newest > current
            ? new ReleaseNews("JingleBox2 " + shown + " is out. This is v" + Shown(current) + ".",
                              StatusKind.Done, latest.Page)
            : null;
    }

    /// <summary>A version read as numbers, or null where there are none to read.</summary>
    /// <remarks>
    /// A missing third number is read as nought, so 2.5 and 2.5.0 are the same release rather
    /// than one being after the other because the runtime counts an absent part as minus one.
    /// </remarks>
    private static Version? Read(string? said)
    {
        var text = (said ?? "").Trim();

        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];

        int cut = text.IndexOfAny(['-', '+']);
        if (cut >= 0) text = text[..cut];

        if (!Version.TryParse(text, out var version)) return null;

        return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
    }

    /// <summary>A version as three numbers, which is how a release is tagged.</summary>
    private static string Shown(Version version) =>
        version.Major + "." + version.Minor + "." + version.Build;
}
