using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Tracker.Effects;
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
public sealed partial class RackEffect : ObservableObject, IRackRow
{
    /// <summary>An effect's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IMachineTint _tint = new MachineTint();

    /// <summary>Shows one effect off the rack. The project itself is held, not copied.</summary>
    /// <param name="effect">The effect this row is about.</param>
    public RackEffect(EffectProject effect) => Effect = effect;

    /// <summary>The effect this row is about, which is the registry's own object.</summary>
    public EffectProject Effect { get; }

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
}
