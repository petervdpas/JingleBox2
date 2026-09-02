using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.ViewModels.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// The presets page for the effect open in the designer.
/// </summary>
/// <remarks>
/// A soundmachine's presets page edits the file, because a soundmachine's preset is a whole
/// instrument: a kit pointing at recordings, a keyboard map, where a take is cut. A form with a
/// box for each of those would be four forms and would still not hold the fifth thing somebody
/// wants next, so that page shows the JSON and lets you work on it.
///
/// An effect's preset is a handful of numbers and can never be anything else, so it gets the form
/// the machine could not have: one row per control, named as the face names it, with the value in
/// a box. Nobody has to see a brace.
///
/// It reads the folder rather than remembering it, since the effect underneath changes whenever
/// somebody opens another one and the folder can be edited by hand while the page is up.
/// </remarks>
public interface ISoundEffectPresetDesk
{
    /// <summary>What the open effect offers, in the order the folder puts them.</summary>
    ObservableCollection<string> Presets { get; }

    /// <summary>The one being worked on, or nothing when none is picked.</summary>
    string? Picked { get; set; }

    /// <summary>True when there is an effect on disc to keep presets in.</summary>
    /// <remarks>
    /// A folder rather than a name, since an effect being built and never saved has a name and
    /// nothing on the disc for a preset to sit beside.
    /// </remarks>
    bool Ready { get; }

    /// <summary>True when a preset is picked, which is what the form and Delete need.</summary>
    bool HasPreset { get; }

    /// <summary>What the picked preset is called, which is what a rename writes.</summary>
    string Called { get; set; }

    /// <summary>One row per control on the face, with where this preset puts it.</summary>
    ObservableCollection<PresetSetting> Settings { get; }

    /// <summary>What the last thing done here did, in the words the page shows.</summary>
    string Said { get; }

    /// <summary>Why the last thing done here did not work, said apart from the rest.</summary>
    string Problem { get; }

    /// <summary>True when there is a refusal to show.</summary>
    bool HasProblem { get; }

    /// <summary>Adds one, holding every control where the panel in the designer has it.</summary>
    /// <remarks>
    /// From the face rather than from the parameter defaults, because the ordinary way to make a
    /// preset is to move the knobs until it sounds right and then keep that.
    /// </remarks>
    IRelayCommand NewCommand { get; }

    /// <summary>Writes the picked one back, name and values together.</summary>
    IRelayCommand SaveCommand { get; }

    /// <summary>Takes the picked one off the shelf.</summary>
    IRelayCommand DeleteCommand { get; }

    /// <summary>Reads the folder again, for the page being opened or the effect changing.</summary>
    void Reread();
}
