using Avalonia.Input;

namespace JingleBox2.Views;

/// <summary>
/// A track being dragged to another place in the song, by the number it is at now.
/// </summary>
/// <remarks>
/// A format of its own rather than a flag on the instrument one, so a drop can tell the two
/// apart by asking rather than by guessing from what happens to be under the pointer: dragging
/// an instrument onto a track points that track at it, and dragging a track moves the track.
/// </remarks>
public static class TrackDragData
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

    /// <summary>What to put in the hand when a drag of that track starts.</summary>
    public static IDataTransfer For(int track)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, new Payload(track)));
        return transfer;
    }

    /// <summary>The track a drag carries, or -1 when it carries something else.</summary>
    public static int IndexFrom(IDataTransfer? transfer) =>
        transfer?.TryGetValue(Format) is Payload payload ? payload.Track : -1;
}
