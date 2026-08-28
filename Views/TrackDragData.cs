using Avalonia.Input;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
public sealed class TrackDragData : IDragPayload
{
    /// <summary>Declared, not composed, so the format is greppable from both ends.</summary>
    private const string FormatName = "jinglebox.track.index";

    /// <summary>
    /// In-process only: the payload never leaves the app, so there is no reason to expose a
    /// track number to other programs or to serialise it.
    /// </summary>
    public static readonly DataFormat<Payload> Format =
        DataFormat.CreateInProcessFormat<Payload>(FormatName);

    /// <summary>A drag format has to carry a reference type, so the number travels wrapped.</summary>
    /// <param name="Track">The number the track is at now, before anything is moved.</param>
    public sealed record Payload(int Track);

    /// <inheritdoc/>
    public IDataTransfer For(int track)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, new Payload(track)));
        return transfer;
    }

    /// <inheritdoc/>
    public int IndexFrom(IDataTransfer? transfer) =>
        transfer?.TryGetValue(Format) is Payload payload ? payload.Track : -1;
}
