using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Views;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>One row in the rack's effects tab: an effect this installation has.</summary>
/// <remarks>
/// It holds the effect itself rather than something made from it, unlike a machine's row, which
/// holds an instrument. There is nothing here to be settings: an effect's knobs stand where the
/// slot on a track's chain left them, and the same effect on two tracks is two sets of values and
/// one of these.
///
/// So the row is what the folder says: its name, its sentence and its colours. Anything a person
/// does to an effect happens on a chain.
/// </remarks>
public sealed partial class RackSoundEffect : ObservableObject, IRackRow
{
    /// <summary>An effect's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IPanelTint _tint = new PanelTint();

    /// <summary>Shows one effect off the rack. The project itself is held, not copied.</summary>
    /// <param name="effect">The effect this row is about.</param>
    public RackSoundEffect(SoundEffectProject effect) => Effect = effect;

    /// <summary>The effect this row is about, which is the registry's own object.</summary>
    public SoundEffectProject Effect { get; }

    /// <summary>Its id, which is what a chain writes down.</summary>
    public string Id => Effect.Id;

    /// <inheritdoc/>
    public string Name => Effect.Name;

    /// <inheritdoc/>
    /// <remarks>The effect's own sentence, which is the only thing there is to say about it here.</remarks>
    public string DetailText => Effect.Summary;

    /// <summary>The effect's own theme, which is what everything about it is painted from.</summary>
    public PanelTheme Theme => Effect.Theme;

    /// <inheritdoc/>
    public string Colour => Theme.Accent;

    /// <inheritdoc/>
    public IBrush Row => _tint.Wash(Theme.Accent, Theme.Row);

    /// <inheritdoc/>
    public IBrush RowOver => _tint.Wash(Theme.Accent, Theme.RowOver);

    /// <inheritdoc/>
    public IBrush RowPicked => _tint.Wash(Theme.Accent, Theme.RowPicked);

    /// <summary>
    /// The effect's face, as the one thing that draws it needs it.
    /// </summary>
    /// <remarks>
    /// Made once and kept, because the panel redraws when it is handed a different face and a
    /// property that built a new one on every read would redraw for ever.
    /// </remarks>
    public Face Shown => _shown ??= new Face(Effect.Panel, Effect.Parameters, Effect.Folder);

    /// <inheritdoc cref="Shown"/>
    private Face? _shown;

    /// <summary>
    /// What its knobs are standing at here, which is a demonstration and is kept nowhere.
    /// </summary>
    /// <remarks>
    /// An effect on the rack is not an effect in use. What one is set to belongs to the slot it
    /// is standing in on some track's chain, and two of the same effect on two tracks are two
    /// sets of values, so there is nothing here for a knob to write into that anybody would want
    /// back. It is the same bench the designer draws its preview on.
    ///
    /// Which is not to say the panel is pointless: this is where a hardware knob is pointed at
    /// the effect, and what a link writes down is the effect's id and the parameter's key rather
    /// than anything about this page.
    /// </remarks>
    public IPanelValues Values => _values ??= new PreviewValues(Knobs);

    /// <summary>
    /// The presets this effect ships, behind the picker on the face drawn beside the list.
    /// </summary>
    /// <remarks>
    /// It applies, unlike a soundmachine's picker on the designer's bench. What an effect's knobs
    /// stand at on the rack is a bench kept nowhere, so putting a preset on it costs nothing and
    /// is the only way to hear what the preset is without putting the effect on a chain first.
    /// </remarks>
    public IPanelPresets Presets => new SoundEffectPresetNames(Effect, Values);

    /// <inheritdoc cref="Values"/>
    private IPanelValues? _values;

    /// <summary>The parameters as the bench holds them, each starting where the effect says.</summary>
    private ObservableCollection<ParameterViewModel> Knobs
    {
        get
        {
            if (_knobs is not null) return _knobs;

            _knobs = new ObservableCollection<ParameterViewModel>();

            foreach (var parameter in Effect.Parameters) _knobs.Add(new ParameterViewModel(parameter));

            return _knobs;
        }
    }

    /// <inheritdoc cref="Knobs"/>
    private ObservableCollection<ParameterViewModel>? _knobs;

    /// <summary>
    /// What its own Menu drops down: the control surfaces pointed at this effect, and learning.
    /// </summary>
    /// <remarks>
    /// The same menu a machine's face carries and the same code behind it, keyed by this effect's
    /// id rather than a machine's. So a template made on one is a template on the other, and the
    /// MIDI CC page cuts its cards by the same rule.
    /// </remarks>
    public IPanelMenu Menu => _menu ??= new Midi.ControlMenu(() => Id, () => Name);

    /// <inheritdoc cref="Menu"/>
    private IPanelMenu? _menu;
}
