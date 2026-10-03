using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Config.Interfaces;
using JingleBox2.Config.Records;

namespace JingleBox2.ViewModels;

/// <summary>One switch on the Tracker or Mixer page in SETTINGS.</summary>
/// <remarks>
/// Reads and writes the one answer in <see cref="IFeatures"/> rather than keeping its own, and is
/// told when that answer moves, so a switch thrown anywhere else shows here too.
/// </remarks>
public sealed partial class FeatureRowViewModel : ObservableObject
{
    /// <summary>The answer this switch reads and writes.</summary>
    private readonly IFeatures _features;

    /// <summary>Takes the part and the answer it is switched in.</summary>
    /// <param name="info">The part.</param>
    /// <param name="features">Where it is switched.</param>
    public FeatureRowViewModel(FeatureInfo info, IFeatures features)
    {
        Info = info;
        _features = features;

        features.Changed += feature =>
        {
            if (feature == info.Feature) OnPropertyChanged(nameof(IsOn));
        };
    }

    /// <summary>The part.</summary>
    public FeatureInfo Info { get; }

    /// <summary>The line on the switch.</summary>
    public string Name => Info.Name;

    /// <summary>What switching it off does.</summary>
    public string Hint => Info.Hint;

    /// <summary>Whether the part is on.</summary>
    public bool IsOn
    {
        get => _features.IsOn(Info.Feature);
        set => _features.Set(Info.Feature, value);
    }
}
