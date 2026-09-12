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

    /// <summary>The settings block, holding the choice among everything else.</summary>
    private readonly ISettingsBlock _settings;

    /// <summary>Takes the settings to keep the choice in, and where the outputs come from.</summary>
    /// <param name="settings">The settings block this installation is running on.</param>
    /// <param name="endpoints">Where the outputs are read from, or the machine's own.</param>
    public SilentOutput(ISettingsBlock settings, IPlaybackEndpoints? endpoints = null)
    {
        _settings = settings;
        _endpoints = endpoints ?? new PlaybackEndpoints().Here();
    }

    /// <inheritdoc/>
    public IReadOnlyList<AudioEndpoint> Outputs => _endpoints.Outputs();

    /// <inheritdoc/>
    public string? Chosen
    {
        get => string.IsNullOrWhiteSpace(_settings.Config.SilentOutput)
            ? null
            : _settings.Config.SilentOutput;
        set
        {
            string chosen = value ?? "";

            if (_settings.Config.SilentOutput == chosen) return;

            _settings.Config.SilentOutput = chosen;

            _settings.Moved();
        }
    }
}
