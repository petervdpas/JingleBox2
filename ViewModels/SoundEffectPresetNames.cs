using System.Collections.Generic;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// A sound effect's own presets, behind the picker on its face.
/// </summary>
/// <remarks>
/// Picking one writes every setting it holds through <see cref="IPanelValues"/> and never into
/// the engine, which is the rule this codebase already paid for once: a value written past the
/// panel's own values moves the sound and leaves every knob on the screen where it was, and from
/// a chair that reads as a preset that did nothing rather than as a picture that is stale.
///
/// Unlike a soundmachine's picker on the designer's bench, this one really applies. A machine's
/// preview has no instrument behind it to apply anything to; an effect's face always has values,
/// whether they are a real engine's on a chain or the bench the rack keeps, so picking a preset
/// there does what picking a preset should.
///
/// The folder is read once, when this is made, and whoever builds it makes a new one each time
/// the panel asks. A preset added while a window is open then turns up without anything having to
/// be wired between the presets page and the face.
/// </remarks>
public sealed class SoundEffectPresetNames : IPanelPresets
{
    /// <summary>What the effect offers, read when this was made.</summary>
    private readonly IReadOnlyList<Records.SoundEffectPresetLine> _presets;

    /// <summary>Where the face reads and writes, which is where a picked preset lands.</summary>
    private readonly IPanelValues? _values;

    /// <summary>Which one is showing.</summary>
    private int _picked = -1;

    /// <summary>Reads that effect's presets, ready to be picked from.</summary>
    /// <param name="effect">The effect whose folder holds them. Nothing offers nothing.</param>
    /// <param name="values">Where a picked preset is written. Left out, picking does nothing.</param>
    /// <param name="shelf">The presets on disc. Left out, the ordinary shelf.</param>
    public SoundEffectPresetNames(
        SoundEffectProject? effect,
        IPanelValues? values = null,
        ISoundEffectPresets? shelf = null)
    {
        _values = values;

        _presets = (shelf ?? new SoundEffectPresets())
            .For(effect)
            .Select(one => new Records.SoundEffectPresetLine(one.Name, one.Settings))
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Names => _presets.Select(one => one.Name).ToList();

    /// <inheritdoc/>
    /// <remarks>
    /// Setting it applies the preset. A number outside the list is taken as none picked rather
    /// than refused, since a picker whose shelf has just shrunk hands one back.
    /// </remarks>
    public int Picked
    {
        get => _picked;
        set
        {
            _picked = value >= 0 && value < _presets.Count ? value : -1;

            if (_picked < 0 || _values is null) return;

            foreach (var (key, standing) in _presets[_picked].Settings)
                _values.Set(key, standing);
        }
    }

    /// <inheritdoc/>
    public string Caption => "Preset";
}
