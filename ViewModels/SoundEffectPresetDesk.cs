using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using JingleBox2.SoundDevices.SoundEffects.Records;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.ViewModels.Records;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
public sealed partial class SoundEffectPresetDesk : ObservableObject, ISoundEffectPresetDesk
{
    /// <summary>What a fresh preset is called before anybody renames it.</summary>
    private const string Fresh = "New preset";

    /// <summary>The shelf on disc.</summary>
    private readonly ISoundEffectPresets _shelf;

    /// <summary>
    /// Whichever effect is open in the designer, asked rather than held.
    /// </summary>
    /// <remarks>
    /// Asked every time, because the effect changes underneath this page and a held one would
    /// have the page writing presets into an effect whose name is no longer at the top of the
    /// window. The same reasoning the utilities page keeps, and for the same reason.
    /// </remarks>
    private readonly Func<SoundEffectProject?> _open;

    /// <summary>
    /// Where the panel in the designer has its controls, or nothing on a page with no panel.
    /// </summary>
    /// <remarks>
    /// A question rather than the values themselves, since the panel is rebuilt whenever the face
    /// is laid out again and anything holding one would be holding the last panel's.
    /// </remarks>
    private readonly Func<IPanelValues?> _face;

    /// <summary>What has been read, so a rename can find the file it came from.</summary>
    private IReadOnlyList<SoundEffectPreset> _read = Array.Empty<SoundEffectPreset>();

    /// <summary>What the picked preset was called when it was picked, for a rename.</summary>
    private string _was = "";

    /// <summary>Builds the page over whichever effect the designer has open.</summary>
    /// <param name="open">The effect being designed, asked each time it matters.</param>
    /// <param name="face">Where the preview panel's controls stand, for a new preset.</param>
    /// <param name="shelf">The presets on disc. Left out, the ordinary shelf.</param>
    public SoundEffectPresetDesk(
        Func<SoundEffectProject?> open,
        Func<IPanelValues?>? face = null,
        ISoundEffectPresets? shelf = null)
    {
        _open = open;
        _face = face ?? (() => null);
        _shelf = shelf ?? new SoundEffectPresets();

        Reread();
    }

    /// <inheritdoc/>
    public ObservableCollection<string> Presets { get; } = new();

    /// <inheritdoc/>
    public ObservableCollection<PresetSetting> Settings { get; } = new();

    /// <inheritdoc/>
    public bool Ready => _open() is { Folder.Length: > 0 };

    /// <inheritdoc/>
    public bool HasPreset => Picked is { Length: > 0 };

    /// <inheritdoc/>
    public bool HasProblem => Problem.Length > 0;

    /// <inheritdoc/>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreset))]
    private string? picked;

    /// <inheritdoc/>
    [ObservableProperty]
    private string called = "";

    /// <inheritdoc/>
    [ObservableProperty]
    private string said = "";

    /// <inheritdoc/>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    private string problem = "";

    /// <inheritdoc/>
    public IRelayCommand NewCommand => new RelayCommand(() =>
    {
        if (_open() is not { } effect) return;

        Problem = "";

        string name = Untaken(Fresh);

        var values = _face();

        var settings = effect.Parameters.ToDictionary(
            one => one.Key,
            one => values is null ? one.Default : values.Get(one.Key),
            StringComparer.Ordinal);

        if (!_shelf.Write(effect, new SoundEffectPreset(name, settings), Presets.Count))
        {
            Problem = "That preset could not be written. Save the effect first.";
            return;
        }

        Reread();

        Picked = name;
        Said = "Added '" + name + "'.";
    });

    /// <inheritdoc/>
    public IRelayCommand SaveCommand => new RelayCommand(() =>
    {
        if (_open() is not { } effect || _was.Length == 0) return;

        Problem = "";

        string name = (Called ?? "").Trim();

        if (name.Length == 0)
        {
            Problem = "A preset needs a name.";
            return;
        }

        if (!string.Equals(name, _was, StringComparison.OrdinalIgnoreCase)
            && Presets.Any(one => string.Equals(one, name, StringComparison.OrdinalIgnoreCase)))
        {
            Problem = "There is already a preset called '" + name + "'.";
            return;
        }

        int at = Math.Max(Presets.IndexOf(_was), 0);

        var settings = Settings.ToDictionary(one => one.Key, one => one.Value, StringComparer.Ordinal);

        if (!string.Equals(name, _was, StringComparison.Ordinal)) _shelf.Remove(effect, _was);

        if (!_shelf.Write(effect, new SoundEffectPreset(name, settings), at))
        {
            Problem = "That preset could not be written.";
            return;
        }

        Reread();

        Picked = name;
        Said = "Saved '" + name + "'.";
    });

    /// <inheritdoc/>
    public IRelayCommand DeleteCommand => new RelayCommand(() =>
    {
        if (_open() is not { } effect || Picked is not { Length: > 0 } name) return;

        Problem = "";

        if (!_shelf.Remove(effect, name))
        {
            Problem = "That preset could not be removed.";
            return;
        }

        Reread();

        Said = "Removed '" + name + "'.";
    });

    /// <inheritdoc/>
    public void Reread()
    {
        string held = Picked ?? "";

        _read = _shelf.For(_open());

        Presets.Clear();

        foreach (var one in _read) Presets.Add(one.Name);

        OnPropertyChanged(nameof(Ready));

        Picked = Presets.Contains(held) ? held : Presets.FirstOrDefault();
    }

    /// <summary>Fills the form whenever another preset is picked out.</summary>
    /// <param name="value">The name now picked, or nothing.</param>
    partial void OnPickedChanged(string? value)
    {
        _was = value ?? "";

        Called = _was;

        Settings.Clear();

        if (_open() is not { } effect) return;

        var preset = _read.FirstOrDefault(one => string.Equals(one.Name, _was, StringComparison.Ordinal));

        foreach (var parameter in effect.Parameters)
            Settings.Add(new PresetSetting
            {
                Key = parameter.Key,
                Name = parameter.Name.Length > 0 ? parameter.Name : parameter.Key,
                Unit = parameter.Unit,
                Min = parameter.Min,
                Max = parameter.Max,
                Step = parameter.Step,
                Value = preset is not null && preset.Settings.TryGetValue(parameter.Key, out double had)
                    ? had
                    : parameter.Default
            });
    }

    /// <summary>
    /// That name, or the next one along that nothing has taken.
    /// </summary>
    /// <remarks>
    /// Pressing New twice should give two presets rather than a refusal, and it is the shelf that
    /// would otherwise silently overwrite: two presets of one name are two files whose numbers
    /// differ and whose names do not.
    /// </remarks>
    /// <param name="wanted">What it would be called if nothing had that name.</param>
    private string Untaken(string wanted)
    {
        if (!Presets.Contains(wanted)) return wanted;

        for (int next = 2; next < 1000; next++)
        {
            string tried = wanted + " " + next.ToString(CultureInfo.InvariantCulture);

            if (!Presets.Contains(tried)) return tried;
        }

        return wanted;
    }
}
