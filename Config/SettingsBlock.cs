using System;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Config;

/// <inheritdoc/>
/// <remarks>
/// A wrapper rather than the document itself, because <see cref="AppConfig"/> is serialised whole
/// and anything added to it lands in everybody's settings file the next time one is written.
/// </remarks>
public sealed class SettingsBlock : ISettingsBlock
{
    /// <inheritdoc/>
    public AppConfig Config { get; }

    /// <summary>Names the settings this run is working over.</summary>
    /// <param name="settings">The document, read from the file at startup.</param>
    public SettingsBlock(AppConfig settings) => Config = settings;

    /// <inheritdoc/>
    public string Name => "Settings";

    /// <inheritdoc/>
    public bool Kept => true;

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public void Moved() => Changed?.Invoke();
}
