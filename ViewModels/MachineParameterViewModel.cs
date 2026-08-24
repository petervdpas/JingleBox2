using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Machines;

namespace JingleBox2.ViewModels;

/// <summary>
/// One of a machine's parameters, as the editor works on it.
/// </summary>
/// <remarks>
/// The parameter itself is plain data in the contract, with no notion of being watched: a
/// machine written by somebody else should not have to take a dependency on a view model
/// toolkit to describe a knob. So the editor wraps it, and every edit goes straight through to
/// the parameter underneath.
///
/// The value is not part of the machine and is not saved. It is what the knob on the preview
/// happens to be showing, the way a shop demonstrator turns a dial: an instrument in a song is
/// where a value belongs.
/// </remarks>
public sealed partial class MachineParameterViewModel : ObservableObject
{
    public MachineParameterViewModel(MachineParameter parameter)
    {
        Parameter = parameter;
        value = parameter.Default;
    }

    public MachineParameter Parameter { get; }

    public string Key
    {
        get => Parameter.Key;
        set { Parameter.Key = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => Parameter.Name;
        set { Parameter.Name = value; OnPropertyChanged(); }
    }

    public string Unit
    {
        get => Parameter.Unit;
        set { Parameter.Unit = value; OnPropertyChanged(); }
    }

    public double Min
    {
        get => Parameter.Min;
        set { Parameter.Min = value; OnPropertyChanged(); }
    }

    public double Max
    {
        get => Parameter.Max;
        set { Parameter.Max = value; OnPropertyChanged(); }
    }

    public double Default
    {
        get => Parameter.Default;
        set { Parameter.Default = value; OnPropertyChanged(); }
    }

    public double Step
    {
        get => Parameter.Step;
        set { Parameter.Step = value; OnPropertyChanged(); }
    }

    /// <summary>Where the preview's knob is standing. Not the machine's, and not saved.</summary>
    [ObservableProperty] private double value;
}
