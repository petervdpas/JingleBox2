using System;
using System.Collections.Generic;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
/// <remarks>
/// A wrapper rather than the list itself, for the reason <see cref="Config.SettingsBlock"/> is
/// one: a block answers for what it holds, and a name and a flag hung on the thing being
/// serialised would land in everybody's file the next time one was written.
/// </remarks>
public sealed class ControlTemplateBlock : IControlTemplateBlock
{
    /// <inheritdoc/>
    public List<ControlTemplate> Templates { get; }

    /// <summary>Names the templates this run is working over.</summary>
    /// <param name="templates">
    /// What was read at startup, or nothing for a run that has none yet, which is a fresh
    /// installation and is the ordinary first morning.
    /// </param>
    public ControlTemplateBlock(List<ControlTemplate>? templates = null) =>
        Templates = templates ?? new List<ControlTemplate>();

    /// <inheritdoc/>
    public string Name => "Control templates";

    /// <inheritdoc/>
    /// <remarks>
    /// Kept, and that is the whole point of them: a template is a fact about your hardware and
    /// the thing it is pointed at, true of every song and worth still being there tomorrow.
    /// </remarks>
    public bool Kept => true;

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public void Moved() => Changed?.Invoke();
}
