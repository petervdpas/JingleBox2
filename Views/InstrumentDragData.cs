using Avalonia.Input;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
public sealed class InstrumentDragData : IDragPayload
{
    /// <summary>Declared, not composed, so the format is greppable from both ends.</summary>
    private const string FormatName = "jinglebox.instrument.index";

    /// <summary>
    /// In-process only: the payload never leaves the app, so there is no reason to expose an
    /// instrument index to other programs or to serialise it.
    /// </summary>
    public static readonly DataFormat<Payload> Format =
        DataFormat.CreateInProcessFormat<Payload>(FormatName);

    /// <summary>A drag format has to carry a reference type, so the index travels wrapped.</summary>
    /// <param name="Index">Where the instrument sits in the song's list.</param>
    public sealed record Payload(int Index);

    /// <inheritdoc/>
    public IDataTransfer For(int instrumentIndex)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, new Payload(instrumentIndex)));
        return transfer;
    }

    /// <inheritdoc/>
    public int IndexFrom(IDataTransfer? transfer) =>
        transfer?.TryGetValue(Format) is Payload payload ? payload.Index : -1;
}
