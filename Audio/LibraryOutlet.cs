using JingleBox2.Audio.Interfaces;
using ManagedBass;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// The library playing the mix itself, which is what happens where nothing else is holding the
/// card: it is opened on the device that was picked and the bus is an ordinary playing channel
/// rather than a decoding one.
///
/// **It is an outlet like the other two rather than the absence of one**, which is the whole of
/// why there is one branch here now instead of three. There is nothing to let go of: the mix
/// stops when the bus is freed, and the bus is freed by whoever made it.
/// </remarks>
public sealed class LibraryOutlet : IMixOutlet
{
    /// <inheritdoc/>
    public bool Pulls => false;

    /// <inheritdoc/>
    public string Word => "";

    /// <inheritdoc/>
    public bool Open(int stream, int rate) => Bass.ChannelPlay(stream);

    /// <inheritdoc/>
    public string Why => Bass.LastError.ToString();

    /// <inheritdoc/>
    public void Close()
    {
    }
}
