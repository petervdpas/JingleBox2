using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using JingleBox2.Audio.Plugins.Bridge;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The patience on the two sockets a plugin's process is spoken to over.
/// </summary>
/// <remarks>
/// This exists because of a fault that made the bridge unusable on Windows for any session
/// longer than half a minute, and looked from a chair exactly like every plugin crashing at
/// once. The listener is given <see cref="PluginBridge.StartTimeoutMilliseconds"/> so a plugin
/// that never connects cannot hold the caller for ever. On Windows a socket handed back by
/// <c>Accept</c> carries a copy of the listening socket's options, so the control socket
/// inherited that patience; on Linux it does not, which is why this was never seen there.
///
/// A control socket is quiet by design, since it carries knob moves and window resizes rather
/// than audio, so a plugin doing its job perfectly said nothing for thirty seconds, the read
/// timed out, and the reader took a timeout for the far end having gone. Measured on a real
/// session: four plugin processes, every one of them declared dead 30.001 seconds after its
/// last control message, and every one still alive to be shut down properly a quarter of a
/// minute later.
///
/// So both halves are pinned here: that the inheritance really is what each platform does,
/// which is the whole reason the line exists, and that a link given
/// <see cref="PluginBridge.WaitForEver"/> outlives the listener's patience. Checked by taking
/// the fix out, which fails the second of these on Windows and neither of them on Linux.
/// </remarks>
public class BridgeSocketTests
{
    /// <summary>A socket path of its own, so two of these can run beside each other.</summary>
    private static string Path() =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                               "jb-test-" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".sock");

    /// <summary>Binds, listens and gives the listener the patience <see cref="PluginProcess"/> gives it.</summary>
    private static Socket Listening(string path, int patience)
    {
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(2);
        listener.ReceiveTimeout = patience;

        return listener;
    }

    /// <summary>Waiting for ever is nought, and nought is the one value that reads as the opposite.</summary>
    [Fact]
    public void WaitingForEverIsNought()
    {
        Assert.Equal(0, PluginBridge.WaitForEver);
    }

    /// <summary>
    /// Whether an accepted socket inherits the listener's patience, which is the platform fact
    /// the fix is answering and differs between the two systems.
    /// </summary>
    /// <remarks>
    /// Asserted rather than assumed, so that a platform changing its mind is reported instead of
    /// quietly turning the line that fixes this into dead weight nobody dares remove.
    /// </remarks>
    [Fact]
    public void AnAcceptedSocketInheritsThePatienceOnWindowsAndNotOnLinux()
    {
        string path = Path();

        using var listener = Listening(path, 12345);
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        client.Connect(new UnixDomainSocketEndPoint(path));

        using var accepted = listener.Accept();

        if (OperatingSystem.IsWindows()) Assert.Equal(12345, accepted.ReceiveTimeout);
        else Assert.Equal(0, accepted.ReceiveTimeout);

        try { File.Delete(path); } catch (Exception) { }
    }

    /// <summary>
    /// A link that says nothing for longer than the listener's patience is still there when it
    /// finally speaks, which is what the control socket has to do for the life of a plugin.
    /// </summary>
    /// <remarks>
    /// This is the regression itself. Without the patience being written over, the receive
    /// throws on Windows the moment the inherited deadline passes, which is what the reader
    /// thread reads as the plugin having gone.
    /// </remarks>
    [Fact]
    public void AQuietLinkOutlivesThePatienceItInherited()
    {
        string path = Path();

        using var listener = Listening(path, 300);
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        client.Connect(new UnixDomainSocketEndPoint(path));

        using var accepted = listener.Accept();

        accepted.ReceiveTimeout = PluginBridge.WaitForEver;

        var sender = new Thread(() =>
        {
            Thread.Sleep(900);
            client.Send(new byte[] { 1, 2, 3, 4, 5 });
        })
        { IsBackground = true };

        sender.Start();

        var buffer = new byte[5];
        int got = accepted.Receive(buffer, 0, 5, SocketFlags.None);

        sender.Join(2000);

        Assert.Equal(5, got);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);

        try { File.Delete(path); } catch (Exception) { }
    }
}
