using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.ViewModels;

namespace JingleBox2.Midi;

/// <summary>
/// The transport on the screen, pressed by a hardware button that was pointed at one of its keys.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="TransportAdapter"/>, and the two are kept apart because they
/// answer different questions. That one is what a device's own transport buttons ask for, in the
/// three words the protocols have; this is what a link asks for, in the four keys a person can
/// see. Folding them together would mean either a protocol gaining a pause it cannot send or a
/// link losing a key that is drawn in front of it.
///
/// A refused key does nothing and says so. The commands guard themselves, so pressing play with
/// nothing to play is already safe, and the line is worth writing because a button that does
/// nothing is otherwise indistinguishable from a link that was never made.
/// </remarks>
/// <param name="transport">The one on the screen, whichever page it is currently patched to.</param>
public sealed class TransportPresses(TransportSwitch transport) : ITransportPresses
{
    /// <inheritdoc/>
    public void Press(TransportKey key)
    {
        Log.Write(LogArea.Midi, () => "transport: a pointed button asks for " + key);

        switch (key)
        {
            case TransportKey.Play: transport.PlayCommand.Execute(null); return;
            case TransportKey.Pause: transport.PauseCommand.Execute(null); return;
            case TransportKey.Stop: transport.StopCommand.Execute(null); return;
            case TransportKey.Record: transport.RecordCommand.Execute(null); return;
        }
    }
}
