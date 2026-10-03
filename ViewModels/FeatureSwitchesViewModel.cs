using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Config;
using JingleBox2.Config.Enums;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// Which parts of the tracker and the mixer are on, as the screens bind to it: a switch per part
/// for each page in SETTINGS, and a property per part for the layouts that hide it.
/// </summary>
/// <remarks>
/// A face over <see cref="IFeatures"/> and nothing more, so a layout and the work behind it read
/// the same answer. Every property is said to have changed whenever any switch moves, since a
/// switch is thrown by hand and seven properties read again is nothing.
/// </remarks>
public sealed class FeatureSwitchesViewModel : ObservableObject
{
    /// <summary>Takes the answer, or makes one with everything on that is kept for this run only.</summary>
    /// <param name="switches">Where the parts are switched.</param>
    public FeatureSwitchesViewModel(IFeatures? switches = null)
    {
        Switches = switches ?? new Features();

        Tracker = Rows(FeaturePage.Tracker);
        Mixer = Rows(FeaturePage.Mixer);

        Switches.Changed += _ => OnPropertyChanged(string.Empty);
    }

    /// <summary>The one answer every switch and every part asks.</summary>
    public IFeatures Switches { get; }

    /// <summary>The switches on the Tracker page.</summary>
    public IReadOnlyList<FeatureRowViewModel> Tracker { get; }

    /// <summary>The switches on the Mixer page.</summary>
    public IReadOnlyList<FeatureRowViewModel> Mixer { get; }

    /// <summary>Whether the automation under the pattern is on.</summary>
    public bool PatternAutomation => Switches.IsOn(Feature.PatternAutomation);

    /// <summary>Whether the command editor is on.</summary>
    public bool CommandEditor => Switches.IsOn(Feature.CommandEditor);

    /// <summary>Whether the neighbouring patterns are drawn.</summary>
    public bool NeighbourPatterns => Switches.IsOn(Feature.NeighbourPatterns);

    /// <summary>Whether a chain's blocks print their readings.</summary>
    public bool ChainReadings => Switches.IsOn(Feature.ChainReadings);

    /// <summary>Whether the song's automation is on.</summary>
    public bool SongAutomation => Switches.IsOn(Feature.SongAutomation);

    /// <summary>Whether the side chain is on.</summary>
    public bool SideChain => Switches.IsOn(Feature.SideChain);

    /// <summary>Whether the patchbay is on.</summary>
    public bool Patchbay => Switches.IsOn(Feature.Patchbay);

    /// <summary>The switches for one page, in the order the catalogue lists them.</summary>
    private IReadOnlyList<FeatureRowViewModel> Rows(FeaturePage page) =>
        Switches.On(page).Select(info => new FeatureRowViewModel(info, Switches)).ToArray();
}
