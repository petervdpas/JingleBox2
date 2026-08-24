using Avalonia.Input;

namespace JingleBox2.Views;

/// <summary>
/// A part of a panel being dragged out of the library and onto a machine.
/// </summary>
/// <remarks>
/// The same arrangement the tracker's drags use, and a format of its own for the same reason:
/// a drop asks what it has been handed rather than guessing from where it landed. In process
/// only, since a kind of element means nothing to another program.
/// </remarks>
public static class MachinePartDragData
{
    /// <summary>Declared, not composed, so the format is greppable from both ends.</summary>
    private const string FormatName = "jinglebox.machine.part";

    public static readonly DataFormat<Payload> Format =
        DataFormat.CreateInProcessFormat<Payload>(FormatName);

    /// <summary>A drag format has to carry a reference type, so the kind travels wrapped.</summary>
    public sealed record Payload(string Kind);

    public static IDataTransfer For(string kind)
    {
        var transfer = new DataTransfer();

        transfer.Add(DataTransferItem.Create(Format, new Payload(kind)));

        return transfer;
    }

    /// <summary>What kind of part a drag carries, or nothing when it carries something else.</summary>
    public static string? KindFrom(IDataTransfer? transfer) =>
        transfer?.TryGetValue(Format) is Payload payload ? payload.Kind : null;
}
