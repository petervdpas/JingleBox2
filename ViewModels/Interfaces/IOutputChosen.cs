using JingleBox2.Audio.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>Where the mix is leaving the machine through.</summary>
/// <remarks>
/// One question, asked by the patchbay so it can name the block the desk feeds. The whole of the
/// output picker is not wanted here: what is drawn is where the sound is going, and the choosing
/// of it belongs to the page that already has that list.
/// </remarks>
public interface IOutputChosen
{
    /// <summary>The output the engine is playing through, or nothing before one has been picked.</summary>
    AudioOutput? SelectedOutputDevice { get; }
}
