using Avalonia.Input;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
public sealed class SlotDrag : ISlotDrag
{
    /// <summary>Declared, not composed, so the format is greppable from both ends.</summary>
    private const string FormatName = "jinglebox.chain.slot";

    /// <inheritdoc cref="ISlotDrag"/>
    public static readonly DataFormat<Payload> Format =
        DataFormat.CreateInProcessFormat<Payload>(FormatName);

    /// <summary>What travels: the chain it came off and where it sat on it.</summary>
    /// <param name="Chain">The chain the device is on, compared by identity.</param>
    /// <param name="Slot">Where it sits now, before anything is moved.</param>
    public sealed record Payload(object Chain, int Slot);

    /// <inheritdoc/>
    public IDataTransfer For(object chain, int index)
    {
        var transfer = new DataTransfer();

        transfer.Add(DataTransferItem.Create(Format, new Payload(chain, index)));

        return transfer;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The chain is compared by reference rather than by anything about it. Two chains can hold
    /// the same effects in the same order and still be two chains, and what is being asked here
    /// is whether this is the one the device was picked up from.
    /// </remarks>
    public int IndexFrom(IDataTransfer? transfer, object chain) =>
        transfer?.TryGetValue(Format) is Payload payload && ReferenceEquals(payload.Chain, chain)
            ? payload.Slot
            : -1;
}
