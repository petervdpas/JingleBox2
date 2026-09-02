using JingleBox2.Rack.SoundDevices.SoundMachines.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// The stand-in the designer shows, so a panel being laid out around a name badge is laid out
/// around one the size a real name is. There is no instrument on the bench to rename, so it says
/// the same thing however hard anybody types at it.
/// </remarks>
public sealed class SoundMachinePreviewName : IInstrumentName
{
    /// <inheritdoc/>
    /// <remarks>
    /// A name somebody might plausibly give an instrument, and not the machine's own: what the
    /// badge shows in use belongs to the song, and a badge laid out around the machine's name
    /// would be laid out around the wrong word.
    /// </remarks>
    public string Said { get; set; } = "Bassline";

    /// <inheritdoc/>
    public bool Fixed => true;
}
