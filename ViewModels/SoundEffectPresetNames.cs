using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using JingleBox2.SoundDevices.SoundEffects.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// A sound effect's own presets and yours, behind the picker on its face.
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
/// **The folder is read every time the names are asked for**, which is when the picker is drawn
/// and when it is told the list moved. A preset saved on the presets page in DESIGNER then turns up
/// the next time a face is drawn, and one kept from the Menu turns up at once. Which one is picked
/// is held as the file it came from rather than as a place in the list, since a preset kept or
/// taken off moves the places of everything after it.
///
/// Keeping takes where each of the effect's controls stands from the values, which is the sound
/// as it is heard, and writes it through the shelf.
/// </remarks>
public sealed class SoundEffectPresetNames : IPanelPresets, IPresetKeeping, INotifyPropertyChanged
{
    /// <summary>The effect whose presets these are.</summary>
    private readonly SoundEffectProject? _effect;

    /// <summary>Where the face reads and writes, which is where a picked preset lands and what a kept one is read from.</summary>
    private readonly IPanelValues? _values;

    /// <summary>The presets on disc.</summary>
    private readonly ISoundEffectPresets _shelf;

    /// <summary>What the effect offers, as last read.</summary>
    private IReadOnlyList<SoundEffectPreset> _presets;

    /// <summary>The file of the one showing, or nothing for none.</summary>
    private string _picked = "";

    /// <summary>Reads that effect's presets, ready to be picked from.</summary>
    /// <param name="effect">The effect whose folder holds them. Nothing offers nothing.</param>
    /// <param name="values">Where a picked preset is written. Left out, picking does nothing and nothing can be kept.</param>
    /// <param name="shelf">The presets on disc. Left out, the ordinary shelf.</param>
    public SoundEffectPresetNames(
        SoundEffectProject? effect,
        IPanelValues? values = null,
        ISoundEffectPresets? shelf = null)
    {
        _effect = effect;
        _values = values;
        _shelf = shelf ?? new SoundEffectPresets();
        _presets = _shelf.For(effect);
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc/>
    /// <remarks>Read off the folder again each time, with yours marked.</remarks>
    public IReadOnlyList<string> Names
    {
        get
        {
            _presets = _shelf.For(_effect);

            return _presets.Select(one => one.Shown).ToList();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Setting it applies the preset. A number outside the list is taken as none picked rather
    /// than refused, since a picker whose shelf has just shrunk hands one back.
    /// </remarks>
    public int Picked
    {
        get => Index(_picked);
        set
        {
            if (value < 0 || value >= _presets.Count)
            {
                _picked = "";
                return;
            }

            var preset = _presets[value];

            _picked = preset.File;

            if (_values is null) return;

            foreach (var (key, standing) in preset.Settings)
                _values.Set(key, standing);
        }
    }

    /// <inheritdoc/>
    public string Caption => "Preset";

    /// <inheritdoc/>
    public bool CanKeep => _effect is { Folder.Length: > 0 } && _values is not null;

    /// <inheritdoc/>
    public string DeviceName => _effect?.Name ?? "";

    /// <inheritdoc/>
    /// <remarks>The preset of yours showing, so keeping again saves over it; otherwise the effect's own name.</remarks>
    public string Suggested => PickedYours ?? DeviceName;

    /// <inheritdoc/>
    public string? PickedYours => Showing is { Yours: true } yours ? yours.Name : null;

    /// <inheritdoc/>
    public string Refusal(string name) =>
        CanKeep ? _shelf.Refusal(_effect, name) : "This effect has nowhere to keep a preset here.";

    /// <inheritdoc/>
    public bool Replaces(string name) => _shelf.Yours(_effect, name) is not null;

    /// <inheritdoc/>
    public bool Keep(string name)
    {
        if (!CanKeep) return false;

        var settings = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var parameter in _effect!.Parameters) settings[parameter.Key] = _values!.Get(parameter.Key);

        if (_shelf.Keep(_effect, name, settings) is not { } kept) return false;

        _presets = _shelf.For(_effect);
        _picked = kept.File;

        Said();

        return true;
    }

    /// <inheritdoc/>
    public bool RemovePicked()
    {
        if (Showing is not { Yours: true } yours || !_shelf.RemoveYours(_effect, yours)) return false;

        _presets = _shelf.For(_effect);
        _picked = "";

        Said();

        return true;
    }

    /// <summary>The preset showing, or nothing.</summary>
    private SoundEffectPreset? Showing => Index(_picked) is >= 0 and var at ? _presets[at] : null;

    /// <summary>Where the preset read from that file is in the list, or -1.</summary>
    private int Index(string file)
    {
        if (file.Length == 0) return -1;

        for (int at = 0; at < _presets.Count; at++)
            if (string.Equals(_presets[at].File, file, StringComparison.Ordinal)) return at;

        return -1;
    }

    /// <summary>Tells the picker on the face that the list moved.</summary>
    private void Said() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Names)));
}
