using Avalonia.Input;

namespace JingleBox2.Views;

/// <summary>
/// The payload for dragging an instrument onto a track. One place that knows the format and
/// how to read it back, so the drag source and the drop target cannot disagree.
/// </summary>
public static class InstrumentDragData
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
    public sealed record Payload(int Index);

    public static IDataTransfer For(int instrumentIndex)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, new Payload(instrumentIndex)));
        return transfer;
    }

    /// <summary>The instrument index a drag carries, or -1 when it carries something else.</summary>
    public static int IndexFrom(IDataTransfer? transfer) =>
        transfer?.TryGetValue(Format) is Payload payload ? payload.Index : -1;
}
