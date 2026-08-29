using Avalonia.Input;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
/// <remarks>
/// A format of its own rather than the track one with a flag on it, for the reason the contract
/// gives: a drop asks whether what is in the hand is its own rather than guessing from what
/// happens to be underneath. An order slot and a track are both a number and would otherwise be
/// indistinguishable, which would mean dragging a track onto the order list appearing to work.
/// </remarks>
public sealed class OrderDragData : IDragPayload
{
    /// <summary>Declared, not composed, so the format is greppable from both ends.</summary>
    private const string FormatName = "jinglebox.order.slot";

    /// <summary>
    /// In-process only: the payload never leaves the app, so there is no reason to expose a
    /// slot number to other programs or to serialise it.
    /// </summary>
    public static readonly DataFormat<Payload> Format =
        DataFormat.CreateInProcessFormat<Payload>(FormatName);

    /// <summary>A drag format has to carry a reference type, so the number travels wrapped.</summary>
    /// <param name="Slot">Where the slot sits in the order now, before anything is moved.</param>
    public sealed record Payload(int Slot);

    /// <inheritdoc/>
    public IDataTransfer For(int slot)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, new Payload(slot)));
        return transfer;
    }

    /// <inheritdoc/>
    public int IndexFrom(IDataTransfer? transfer) =>
        transfer?.TryGetValue(Format) is Payload payload ? payload.Slot : -1;
}
