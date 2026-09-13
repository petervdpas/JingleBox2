namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// A picker that can keep what its device sounds like now as a preset of your own, and take one off.
/// </summary>
/// <remarks>
/// What the Menu's two preset lines need, and nothing more, so one set of lines serves a
/// soundmachine and an effect. The picker is the one to answer because the picker knows which
/// preset is showing and has to show the new one; where the files are is its shelf's business.
/// </remarks>
public interface IPresetKeeping
{
    /// <summary>Whether a preset can be kept at all: a device installed here, with somewhere to keep one.</summary>
    bool CanKeep { get; }

    /// <summary>What the device is called, for the words around keeping a preset.</summary>
    string DeviceName { get; }

    /// <summary>The name a preset kept now would start with.</summary>
    string Suggested { get; }

    /// <summary>The name of the preset of yours that is showing, or nothing when what is showing is not yours.</summary>
    string? PickedYours { get; }

    /// <summary>Why that name cannot be kept under, or nothing when it can.</summary>
    /// <param name="name">What somebody typed.</param>
    string Refusal(string name);

    /// <summary>Whether keeping under that name would replace a preset of yours.</summary>
    /// <param name="name">What somebody typed.</param>
    bool Replaces(string name);

    /// <summary>Keeps the sound as a preset of yours and shows it as picked, without putting it on again.</summary>
    /// <param name="name">What to call it.</param>
    /// <returns>Whether it was kept.</returns>
    bool Keep(string name);

    /// <summary>Takes the preset of yours that is showing off. The sound is left as it is.</summary>
    /// <returns>Whether it was taken off.</returns>
    bool RemovePicked();
}
