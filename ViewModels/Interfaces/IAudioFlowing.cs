using JingleBox2.UI.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>What this application is carrying audio through at this moment.</summary>
/// <remarks>
/// Asked of the one thing that holds every half of this program at once, since no page can see
/// the pads, the song, the takes and the input together. Read off the meters that are already
/// running rather than from a second set of measurements: where the threshold sits is the
/// business of whoever is measuring, and everything above only wants to know yes or no.
/// </remarks>
public interface IAudioFlowing
{
    /// <summary>What is sounding right now, one answer per path.</summary>
    PatchSignals Signals { get; }
}
