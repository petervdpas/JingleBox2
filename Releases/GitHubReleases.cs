using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Releases.Interfaces;
using JingleBox2.Releases.Records;

namespace JingleBox2.Releases;

/// <inheritdoc/>
/// <remarks>
/// The releases are published on GitHub, and its API answers the newest one that is neither a
/// draft nor a pre-release, which is exactly the question. Asked with a name, since GitHub
/// refuses a request that carries none, and given ten seconds, since this runs while somebody
/// is starting to work and a check that hangs on a bad connection is worse than no check.
/// </remarks>
public sealed class GitHubReleases : IReleaseFeed
{
    /// <summary>The newest release of this repository.</summary>
    private const string Address = "https://api.github.com/repos/petervdpas/JingleBox2/releases/latest";

    /// <summary>How long the asking may take before nothing is known.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public async Task<Release?> Latest(CancellationToken cancel = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = Patience };
            using var request = new HttpRequestMessage(HttpMethod.Get, Address);

            request.Headers.UserAgent.ParseAdd("JingleBox2");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.SendAsync(request, cancel).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Write(LogArea.App, () => "releases: asked and heard " + (int)response.StatusCode);
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancel).ConfigureAwait(false);

            var root = json.RootElement;

            if (!root.TryGetProperty("tag_name", out var tag) || tag.GetString() is not { Length: > 0 } name)
                return null;

            var page = root.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "";

            Log.Write(LogArea.App, () => "releases: the latest is " + name);

            return new Release(name, page);
        }
        catch (Exception e)
        {
            Log.Write(LogArea.App, () => "releases: could not ask: " + e.Message);
            return null;
        }
    }
}
