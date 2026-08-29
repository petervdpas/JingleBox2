
namespace JingleBox2.ViewModels.Records;

/// <summary>
/// Something on the machine a preset could be given a value for, but has not yet.
/// </summary>
/// <param name="Kind">
/// Which sort of thing on the machine it is: a pad, a knob, a fader, a recording. The kind the
/// machine's own description gave it, so the list can be narrowed to one sort at a time.
/// </param>
/// <param name="Fresh">
/// True for the offer that makes one more of something rather than filling in a named thing.
/// A machine that does not declare its things has one of these and no list, since how many there
/// are is what the preset decides.
/// </param>
/// <param name="Key">What the machine calls it, which is the key a preset writes it under.</param>
/// <param name="Said">What the page shows for it.</param>
public sealed record PresetOffer(string Key, string Said, string Kind, bool Fresh = false)
{
    /// <summary>What the page shows, for a picker with no template.</summary>
    public override string ToString() => Said;
}
