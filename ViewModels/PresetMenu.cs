using System.Collections.Generic;
using System.Threading.Tasks;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The two lines of a soundmachine's Menu that keep a preset of your own and take one off.
/// </summary>
/// <remarks>
/// Save as preset is live wherever the machine keeps presets at all, and Delete this preset only
/// while the preset showing is one of yours, since the machine's own come back whenever it is
/// brought up to date. Both lines are there either way: a line that goes missing reads as the
/// feature being gone, where a grey one with a tip says why not now.
///
/// Each press asks its questions and does its work in that order and nothing else. A name that is
/// refused is said and nothing is written; a name already used by one of yours is asked about
/// before it is replaced; a deletion is always asked about, since it is somebody's work.
/// </remarks>
/// <param name="presets">The picker whose instrument is kept and whose list changes.</param>
/// <param name="questions">What is asked. Left out, the application's dialogs.</param>
public sealed class PresetMenu(InstrumentPresets presets, IPresetQuestions? questions = null) : IPanelMenu
{
    /// <summary>What is asked.</summary>
    private readonly IPresetQuestions _questions = questions ?? new PresetQuestions();

    /// <summary>The line that keeps one.</summary>
    public const string SaveLine = "Save as preset...";

    /// <summary>The line that takes one off.</summary>
    public const string DeleteLine = "Delete this preset";

    /// <inheritdoc/>
    public IReadOnlyList<PanelMenuItem> Read()
    {
        var yours = presets.PickedYours;

        return new[]
        {
            new PanelMenuItem(SaveLine)
            {
                Tip = presets.CanKeep
                    ? "Keeps what this " + presets.MachineName + " sounds like now as a preset of your own, beside the ones it ships with."
                    : "This machine starts from your recordings, so there is no preset to keep.",
                Option = MenuOptionWords.Presets,
                Live = presets.CanKeep,
                Chosen = () => _ = Save()
            },
            new PanelMenuItem(DeleteLine)
            {
                Tip = yours is null
                    ? "Only a preset of your own can be deleted. Pick one of yours, marked with a star, first."
                    : "Deletes your preset '" + yours.Name + "'. The sound on this instrument stays as it is.",
                Option = MenuOptionWords.Presets,
                Live = yours is not null,
                Chosen = () => _ = Delete()
            }
        };
    }

    /// <summary>Asks for a name and keeps the sound under it.</summary>
    /// <returns>Whether a preset was kept.</returns>
    public async Task<bool> Save()
    {
        if (!presets.CanKeep) return false;

        string? name = await _questions.Name(presets.MachineName, presets.Suggested);

        if (string.IsNullOrWhiteSpace(name)) return false;

        if (presets.Refusal(name) is { Length: > 0 } why)
        {
            await _questions.Refused(why);

            return false;
        }

        if (presets.Replaces(name) && !await _questions.Replace(name.Trim())) return false;

        if (presets.Keep(name)) return true;

        await _questions.Refused("The preset could not be written. The log says why.");

        return false;
    }

    /// <summary>Asks, and takes the preset of yours that is showing off the machine.</summary>
    /// <returns>Whether it was taken off.</returns>
    public async Task<bool> Delete()
    {
        if (presets.PickedYours is not { } yours) return false;

        if (!await _questions.Delete(yours.Name)) return false;

        return presets.RemovePicked();
    }
}
