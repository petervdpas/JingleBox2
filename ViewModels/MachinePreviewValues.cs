using JingleBox2.Rack.Faces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.Rack.Faces.Interfaces;

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
/// <param name="parameters">
/// The parameters the editor is showing, which are both the description and the store here.
/// </param>
public sealed class MachinePreviewValues(ObservableCollection<MachineParameterViewModel> parameters) : IPanelValues
{
    /// <summary>
    /// Raised when a value here moved, so the panel drawing them follows.
    /// </summary>
    /// <remarks>
    /// The same event <see cref="PanelValues"/> raises, and needed for the same reason: on the
    /// rack a knob can be pointed at a machine and turned from the desk, and without this the
    /// panel would never hear about it. This class does not inherit that one because the editor's
    /// values are the parameters it is showing rather than a machine's settings.
    /// </remarks>
    public event System.Action<string>? Said;

    /// <inheritdoc/>
    /// <remarks>Nought for a key no parameter answers to, since the bench has nothing else to say.</remarks>
    public double Get(string key) => Find(key)?.Value ?? 0;

    /// <inheritdoc/>
    /// <remarks>
    /// A write that would not move the knob is dropped rather than announced. A panel reads
    /// itself again whenever it hears <see cref="Said"/>, and a controller resting against an end
    /// sends the same number over and over.
    /// </remarks>
    public void Set(string key, double value)
    {
        var parameter = Find(key);

        if (parameter is null) return;

        if (System.Math.Abs(parameter.Value - value) < 1e-9) return;

        parameter.Value = value;

        Said?.Invoke(key);
    }

    /// <summary>
    /// The settings that are not numbers, which in the editor are whatever was last put there.
    /// </summary>
    /// <remarks>
    /// A machine holds more than values: which recording it plays is a name. The editor has no
    /// machine to hold one, so a dictionary stands in, and a take picked while laying out a
    /// panel is remembered only for as long as the panel is being laid out.
    /// </remarks>
    private readonly Dictionary<string, string> texts = new();

    /// <inheritdoc/>
    public string GetText(string key) => texts.TryGetValue(key, out string? held) ? held : "";

    /// <inheritdoc/>
    public void SetText(string key, string value)
    {
        if (texts.TryGetValue(key, out string? was) && was == value) return;

        texts[key] = value;

        Said?.Invoke(key);
    }

    /// <summary>The parameter with that key, or null when the panel names one the machine has not got.</summary>
    /// <remarks>
    /// A walk rather than a dictionary: a machine has a few dozen parameters at most, and the
    /// list is being edited underneath, so anything cached would go stale as a knob was added.
    /// </remarks>
    private MachineParameterViewModel? Find(string key) =>
        parameters.FirstOrDefault(p => p.Key == key);
}
