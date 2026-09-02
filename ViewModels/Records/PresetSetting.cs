using CommunityToolkit.Mvvm.ComponentModel;

namespace JingleBox2.ViewModels.Records;

/// <summary>
/// One control of a face, and where a preset puts it.
/// </summary>
/// <remarks>
/// A class rather than a record, and observable, because the value is typed into a box and the
/// page has to hear it: a record would be replaced on every keystroke and the row underneath the
/// cursor would be rebuilt while somebody was still typing in it.
///
/// It carries the parameter's own ends as well as its value, so the box can refuse a number
/// outside them where it is typed rather than letting the shelf quietly bring it inside on the
/// way to disc.
/// </remarks>
public sealed partial class PresetSetting : ObservableObject
{
    /// <summary>Where this preset puts the control.</summary>
    [ObservableProperty]
    private double value;

    /// <summary>What files call it, which never changes.</summary>
    public string Key { get; init; } = "";

    /// <summary>What the face calls it, which is what the row shows.</summary>
    public string Name { get; init; } = "";

    /// <summary>What it is measured in, or nothing.</summary>
    public string Unit { get; init; } = "";

    /// <summary>The lowest it goes.</summary>
    public double Min { get; init; }

    /// <summary>The highest it goes.</summary>
    public double Max { get; init; } = 1;

    /// <summary>How far one nudge moves it.</summary>
    public double Step { get; init; } = 0.01;
}
