using System.Collections.Generic;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Config;

/// <inheritdoc/>
/// <remarks>
/// Over the settings file, which is where everything about this installation rather than about a
/// song already lives. The list itself is read from the machine each time it is asked for: an
/// output is plugged in and unplugged while the application runs, and a cable installed since it
/// started should be there without a restart.
/// </remarks>
public sealed class SilentOutput : ISilentOutput
{
    /// <summary>Where the outputs are read from.</summary>
    private readonly IPlaybackEndpoints _endpoints;

    /// <summary>The settings, holding the choice among everything else.</summary>
    private readonly AppConfig _cfg;

    /// <summary>What writes them out.</summary>
    private readonly IConfigStore _store;

    /// <summary>Takes the settings to keep the choice in, and where the outputs come from.</summary>
    /// <param name="cfg">The settings this installation is running on.</param>
    /// <param name="store">What puts them on the disc.</param>
    /// <param name="endpoints">Where the outputs are read from, or the machine's own.</param>
    public SilentOutput(AppConfig cfg, IConfigStore store, IPlaybackEndpoints? endpoints = null)
    {
        _cfg = cfg;
        _store = store;
        _endpoints = endpoints ?? new PlaybackEndpoints().Here();
    }

    /// <inheritdoc/>
    public IReadOnlyList<AudioEndpoint> Outputs => _endpoints.Outputs();

    /// <inheritdoc/>
    public string? Chosen
    {
        get => string.IsNullOrWhiteSpace(_cfg.SilentOutput) ? null : _cfg.SilentOutput;
        set
        {
            string chosen = value ?? "";

            if (_cfg.SilentOutput == chosen) return;

            _cfg.SilentOutput = chosen;
            _store.Save(_cfg);
        }
    }
}
