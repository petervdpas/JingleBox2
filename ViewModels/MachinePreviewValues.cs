using JingleBox2.Machines;
using System.Collections.ObjectModel;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// The values the editor's panel shows: a demonstration, kept nowhere.
/// </summary>
/// <remarks>
/// A panel has to read and write something, and in a song that something is the instrument's
/// settings. In the editor there is no instrument: the machine is being built, and turning a
/// knob on it is somebody trying the controls in a shop. So the values live in the parameters
/// the editor is showing and go no further, and every one of them starts where the machine says
/// it should.
/// </remarks>
public sealed class MachinePreviewValues(ObservableCollection<MachineParameterViewModel> parameters) : IMachineValues
{
    public double Get(string key) => Find(key)?.Value ?? 0;

    public void Set(string key, double value)
    {
        var parameter = Find(key);

        if (parameter != null) parameter.Value = value;
    }

    private MachineParameterViewModel? Find(string key) =>
        parameters.FirstOrDefault(p => p.Key == key);
}
