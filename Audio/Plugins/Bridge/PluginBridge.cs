using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net.Sockets;
using System.Text;
using JingleBox2.Audio.Plugins.Bridge.Enums;
using JingleBox2.Audio.Plugins.Bridge.Interfaces;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins.Bridge;

/// <summary>
/// What the two processes say to each other, and where they put the audio.
/// </summary>
/// <remarks>
/// A plugin runs in a process of its own, so that a plugin which falls over takes nothing with
/// it but itself. This is the wire between the two: a socket for everything that is said, and a
/// block of shared memory for the audio, because a stereo block sixty times a second is not
/// something to be sending down a pipe.
///
/// Both halves are compiled into the same executable. The child is this same program started
/// again with <see cref="HostArgument"/>, which is why there is no second binary to ship, and
/// why the child is always exactly the same build as the parent.
/// </remarks>
internal static class PluginBridge
{
    /// <summary>The argument that turns this program into a plugin's process.</summary>
    public const string HostArgument = "--plugin-host";

    /// <summary>The argument that makes it read a folder of plugins and say what is in it.</summary>
    public const string ScanArgument = "--plugin-scan";

    /// <summary>Set to 1 to load plugins in this process after all, for a bug that needs it.</summary>
    public const string InProcessVariable = "JB_PLUGINS_INPROCESS";

    /// <summary>Set to 1 to have the child write what it is doing to the log.</summary>
    public const string TraceVariable = "JB_PLUGIN_TRACE";

    /// <summary>
    /// Where the child should write its log. Passed rather than worked out again, so the two
    /// processes cannot disagree about where the log lives.
    /// </summary>
    public const string LogFolderVariable = "JB_LOG_DIR";

    /// <summary>Written at the top of the shared block, so a stale file is spotted rather than played.</summary>
    /// <remarks>
    /// The four bytes spell JBAG. A block file left behind by a process that was killed still
    /// exists and still maps, so the child reads this first and refuses anything that is not
    /// ours: mapping somebody else's file and playing whatever is in it is a noise nobody
    /// should have to hear twice.
    /// </remarks>
    public const int Magic = 0x4A424147;

    /// <summary>Every plugin here is stereo in and stereo out.</summary>
    public const int Channels = 2;

    /// <summary>Parameter moves and notes waiting for the next block. Beyond this they are dropped.</summary>
    public const int MaxEvents = 1024;

    /// <summary>One event: what kind, which parameter or note, and how much.</summary>
    /// <remarks>
    /// Sixteen bytes, four fields of four: the kind, the id of the parameter or the number of
    /// the note, the value as a float, and one spare integer. Fixed size on purpose, since the
    /// reader works out where a slot is by multiplying rather than by walking to it.
    /// </remarks>
    public const int EventSize = 16;

    /// <summary>Where the events start, past the header.</summary>
    /// <remarks>
    /// Sixty four bytes of header, of which the magic, the frame count, the channel count and
    /// the ring's two indexes are what is used. See <see cref="BridgeBlock"/> for the offsets.
    /// </remarks>
    public const int EventsOffset = 64;

    /// <summary>Where the audio starts, past the events.</summary>
    public const int AudioOffset = EventsOffset + MaxEvents * EventSize;

    /// <summary>How long the audio thread waits for a block before giving up on the plugin.</summary>
    public const int BlockTimeoutMilliseconds = 1000;

    /// <summary>The same, for the first block, which is where a plugin does its lazy loading.</summary>
    public const int FirstBlockTimeoutMilliseconds = 8000;

    /// <summary>How long to wait for the child to answer a question that is not audio.</summary>
    public const int CallTimeoutMilliseconds = 20000;

    /// <summary>
    /// How long an ordinary question waits: what a knob is set to, what it reads as, the list
    /// of them.
    /// </summary>
    /// <remarks>
    /// These are asked from the thread that draws, so the patience for them is the patience a
    /// window has before somebody says it has frozen. A plugin that has not answered in three
    /// seconds is not going to answer, and waiting twenty for it turns one sick plugin into an
    /// application that will not repaint. What is kept from the long patience is loading and
    /// handing over a patch, which are slow for honest reasons: Vital's state runs to hundreds
    /// of kilobytes.
    /// </remarks>
    public const int QuickTimeoutMilliseconds = 3000;

    /// <summary>
    /// The same, for anything to do with the plugin's own window.
    /// </summary>
    /// <remarks>
    /// Shorter, because the plugin's window and its answers are on the same thread over there,
    /// so a plugin whose interface has locked up stops answering. Waiting twenty seconds on
    /// that would be twenty seconds of a frozen application; giving up on it after eight is a
    /// message saying the plugin stopped, which is both true and something to act on.
    /// </remarks>
    public const int WindowTimeoutMilliseconds = 8000;

    /// <summary>How long the child is given to start up and say hello.</summary>
    public const int StartTimeoutMilliseconds = 30000;

    /// <summary>
    /// No patience at all, which is a socket that waits until something arrives.
    /// </summary>
    /// <remarks>
    /// Written down rather than left as a bare nought, because nought reads as no waiting
    /// whatever and means the opposite. It is what the control socket is set to once it has been
    /// accepted, and the reason is in <see cref="PluginProcess.Start"/>: on Windows an accepted
    /// socket inherits the listener's timeout, and a control socket is quiet by design, so an
    /// inherited patience meant for a plugin that never connects would fire on one that is
    /// merely working.
    /// </remarks>
    public const int WaitForEver = 0;

    /// <summary>How big the shared block has to be for this many frames.</summary>
    /// <remarks>
    /// The header and the events, then the audio twice over: the input and the output are two
    /// separate stretches, so nothing has to be copied out of the way before the plugin writes
    /// its answer. A block asked for with nothing in it is still given room for one frame,
    /// since a mapping of nought bytes is not a mapping.
    /// </remarks>
    public static long BlockBytes(int maxFrames) =>
        AudioOffset + (long)Math.Max(1, maxFrames) * Channels * sizeof(float) * 2;

    /// <summary>Where the input audio sits inside the block.</summary>
    public const int InputOffset = AudioOffset;

    /// <summary>Where the output audio sits inside the block.</summary>
    public static int OutputOffset(int maxFrames) => AudioOffset + maxFrames * Channels * sizeof(float);
}



/// <summary>
/// One message on the socket: a length, a kind, and whatever that kind carries.
/// </summary>
/// <remarks>
/// Both sides read and write with the same class, so a message can only be misunderstood if it
/// is misunderstood twice in the same way. Everything is little-endian because both ends of
/// this wire are the same machine.
/// </remarks>
internal sealed class BridgeLink : IDisposable
{
    /// <summary>The socket this link speaks over. A process has two: one for audio, one for the rest.</summary>
    private readonly Socket _socket;

    /// <summary>
    /// Held while a message is written, since anything may send: the drawing thread asking a
    /// question, the plugin's own thread reporting a knob. Two sends interleaving would put
    /// half of one message inside the other and there is no recovering from that.
    /// </summary>
    private readonly object _write = new();

    /// <summary>
    /// The five header bytes, read into again for every message.
    /// </summary>
    /// <remarks>
    /// Only the one reader thread ever touches this, which is what makes reusing it safe. The
    /// writer builds a header of its own inside the lock rather than sharing this one, because
    /// a send can come from any thread while a read is in progress.
    /// </remarks>
    private readonly byte[] _header = new byte[5];

    /// <summary>Wraps a socket that is already connected to the other half.</summary>
    public BridgeLink(Socket socket) => _socket = socket;

    /// <summary>
    /// The socket underneath. The audio path reads and writes its own eight bytes rather than
    /// going through a message, so it needs the socket itself.
    /// </summary>
    public Socket Socket => _socket;

    /// <summary>True while the other end is still there.</summary>
    public bool Connected => _socket.Connected;

    /// <summary>
    /// Writes one message: four bytes of length, then one byte saying what it is, then the body.
    /// </summary>
    /// <remarks>
    /// A socket takes as much as it feels like taking, so the write is looped until all of it
    /// has gone. A socket that has closed underneath throws, deliberately: the caller is the
    /// one that knows whether that means the plugin has died or the process is shutting down.
    /// </remarks>
    public void Send(BridgeCall call, byte[]? payload = null)
    {
        int length = payload?.Length ?? 0;

        lock (_write)
        {
            var header = new byte[5];
            header[0] = (byte)(length & 0xFF);
            header[1] = (byte)((length >> 8) & 0xFF);
            header[2] = (byte)((length >> 16) & 0xFF);
            header[3] = (byte)((length >> 24) & 0xFF);
            header[4] = (byte)call;

            SendAll(header, header.Length);

            if (length > 0) SendAll(payload!, length);
        }
    }

    /// <summary>Waits for the next message. Null when the other end has gone.</summary>
    /// <remarks>
    /// A length that is negative or beyond sixty four megabytes is read as the wire having gone
    /// wrong rather than as an enormous message, and the link is treated as closed. The biggest
    /// honest payload here is a plugin's own patch, which runs to hundreds of kilobytes.
    /// </remarks>
    public (BridgeCall Call, byte[] Payload)? Receive()
    {
        if (!ReadAll(_header, 5)) return null;

        int length = _header[0] | (_header[1] << 8) | (_header[2] << 16) | (_header[3] << 24);
        var call = (BridgeCall)_header[4];

        if (length < 0 || length > 64 * 1024 * 1024) return null;

        var payload = length == 0 ? Array.Empty<byte>() : new byte[length];

        if (length > 0 && !ReadAll(payload, length)) return null;

        return (call, payload);
    }

    /// <summary>Writes the whole buffer, in as many goes as the socket wants to take it.</summary>
    private void SendAll(byte[] buffer, int count)
    {
        int sent = 0;

        while (sent < count)
        {
            int wrote = _socket.Send(buffer, sent, count - sent, SocketFlags.None);
            if (wrote <= 0) throw new IOException("the plugin bridge closed while writing");
            sent += wrote;
        }
    }

    /// <summary>
    /// Reads exactly that many bytes, or answers false because there is nobody left to read
    /// from. A socket that has been disposed underneath a waiting read is the ordinary way this
    /// ends, so that is an answer rather than an exception.
    /// </summary>
    private bool ReadAll(byte[] buffer, int count)
    {
        int read = 0;

        while (read < count)
        {
            int got;

            try
            {
                got = _socket.Receive(buffer, read, count - read, SocketFlags.None);
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            if (got <= 0) return false;
            read += got;
        }

        return true;
    }

    /// <summary>
    /// Shuts the socket down both ways and lets it go. Both halves are wrapped, since a socket
    /// whose other end has already died throws on the way out and there is nothing to do about
    /// it that is not being done.
    /// </summary>
    public void Dispose()
    {
        try { _socket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
        try { _socket.Dispose(); } catch (Exception) { }
    }
}

/// <inheritdoc/>
/// <remarks>
/// Public, as its contract is. Everything else in this file is internal because nothing outside
/// the assembly has any business with a socket or a block of shared memory, but the shape of a
/// message is the one part of the bridge worth being able to put a question to from outside,
/// and an internal implementation behind a public interface is a contract nobody can hold.
/// </remarks>
public sealed class BridgeBody : IBridgeBody
{
    /// <inheritdoc/>
    public byte[] Words(params string[] words)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        writer.Write(words.Length);
        foreach (var word in words) writer.Write(word ?? "");

        return stream.ToArray();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The list is grown rather than made to the size the payload asks for, and every read is
    /// inside one try. Both used to be the other way round and both could take the host down
    /// from a message it was merely reading. A count is four bytes off a wire, so a damaged one
    /// asked for an array of two thousand million strings and the host died of it; and reading
    /// past the end of a truncated payload throws, which is precisely what a payload from a
    /// process that has just crashed looks like. The bridge exists so that a plugin falling over
    /// takes nothing with it but itself, and those two were the way through it.
    /// </remarks>
    public string[] ReadWords(byte[] payload)
    {
        var words = new List<string>();

        try
        {
            using var stream = new MemoryStream(payload);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            int count = reader.ReadInt32();

            for (int index = 0; index < count; index++) words.Add(reader.ReadString());
        }
        catch (Exception)
        {
        }

        return words.ToArray();
    }

    /// <inheritdoc/>
    public byte[] Number(uint id, double value)
    {
        var payload = new byte[12];

        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), id);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 8), value);

        return payload;
    }

    /// <inheritdoc/>
    public (uint Id, double Value) ReadNumber(byte[] payload) =>
        payload.Length < 12
            ? (0u, 0d)
            : (BitConverter.ToUInt32(payload, 0), BitConverter.ToDouble(payload, 4));

    /// <inheritdoc/>
    public byte[] Double(double value) => BitConverter.GetBytes(value);

    /// <inheritdoc/>
    public double ReadDouble(byte[] payload) =>
        payload.Length < 8 ? 0d : BitConverter.ToDouble(payload, 0);

    /// <inheritdoc/>
    public byte[] Pair(int first, int second)
    {
        var payload = new byte[8];

        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), first);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 4), second);

        return payload;
    }

    /// <inheritdoc/>
    public (int First, int Second) ReadPair(byte[] payload) =>
        payload.Length < 8 ? (0, 0) : (BitConverter.ToInt32(payload, 0), BitConverter.ToInt32(payload, 4));

    /// <inheritdoc/>
    public byte[] Three(int first, int second, int third)
    {
        var payload = new byte[12];

        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), first);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 4), second);
        BitConverter.TryWriteBytes(payload.AsSpan(8, 4), third);

        return payload;
    }

    /// <inheritdoc/>
    public (int First, int Second, int Third) ReadThree(byte[] payload) =>
        payload.Length < 12
            ? (0, 0, 0)
            : (BitConverter.ToInt32(payload, 0), BitConverter.ToInt32(payload, 4), BitConverter.ToInt32(payload, 8));

    /// <inheritdoc/>
    public byte[] Handle(nint window) => BitConverter.GetBytes((long)window);

    /// <inheritdoc/>
    public nint ReadHandle(byte[] payload) =>
        payload.Length < 8 ? 0 : (nint)BitConverter.ToInt64(payload, 0);

    /// <inheritdoc/>
    public byte[] Parameters(System.Collections.Generic.IReadOnlyList<PluginParameter> parameters)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        writer.Write(parameters.Count);

        foreach (var parameter in parameters)
        {
            writer.Write(parameter.Id);
            writer.Write(parameter.Name ?? "");
            writer.Write(parameter.Minimum);
            writer.Write(parameter.Maximum);
            writer.Write(parameter.Default);
            writer.Write(parameter.Steps);
            writer.Write(parameter.IsHidden);
            writer.Write(parameter.IsReadOnly);
            writer.Write(parameter.IsBypass);
            writer.Write(parameter.Normalized);
            writer.Write(parameter.Units ?? "");
        }

        return stream.ToArray();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Grown rather than made to the count the payload asks for, and read inside one try, for
    /// the same two reasons <see cref="ReadWords"/> is. This is the longest message the bridge
    /// carries, since Serum answers with 2622 of these, so it is the one most likely to be
    /// caught half sent by a plugin falling over, and it is the one where a count off a damaged
    /// wire buys the largest array. What comes back is however many were whole.
    /// </remarks>
    public PluginParameter[] ReadParameters(byte[] payload)
    {
        var parameters = new List<PluginParameter>();

        try
        {
            using var stream = new MemoryStream(payload);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            int count = reader.ReadInt32();

            for (int index = 0; index < count; index++)
            {
                parameters.Add(new PluginParameter(
                    reader.ReadUInt32(),
                    reader.ReadString(),
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadInt32(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadString()));
            }
        }
        catch (Exception)
        {
        }

        return parameters.ToArray();
    }
}

/// <summary>
/// The block of memory both processes can see: the audio, and whatever is queued to go with it.
/// </summary>
/// <remarks>
/// A file rather than a name, because a named shared mapping is a Windows idea and this has to
/// work on Linux first. The file lives where the system keeps things that are not really files,
/// so it never touches a disk.
///
/// The layout, from the start of the block: the magic at 0, the frames a block may hold at 4,
/// the channel count at 8, the ring's write index at 16 and its read index at 20. The events
/// begin at <see cref="PluginBridge.EventsOffset"/>, the input audio at
/// <see cref="PluginBridge.InputOffset"/>, and the output audio after it at
/// <see cref="PluginBridge.OutputOffset"/>. Both sides work these out from the same constants,
/// so the layout is agreed rather than negotiated.
/// </remarks>
internal sealed unsafe class BridgeBlock : IDisposable
{
    /// <summary>The mapping itself, held so it stays mapped.</summary>
    private readonly MemoryMappedFile _file;

    /// <summary>The window onto it, which is where the pointer comes from.</summary>
    private readonly MemoryMappedViewAccessor _view;

    /// <summary>
    /// The file behind the mapping, for the side that made it, and null for the side that only
    /// opened it. Whoever holds a path is the one who takes the file away again.
    /// </summary>
    private readonly string? _path;

    /// <summary>
    /// The start of the block, taken once and held. Everything here is pointer arithmetic off
    /// this: the audio thread copies a stereo block in and out sixty times a second, and going
    /// through the accessor's own read and write methods for that is not affordable.
    /// </summary>
    private byte* _base;

    /// <summary>Wraps a mapping that is already open and takes a pointer to the start of it.</summary>
    private BridgeBlock(MemoryMappedFile file, MemoryMappedViewAccessor view, string? path, int maxFrames)
    {
        _file = file;
        _view = view;
        _path = path;

        MaxFrames = maxFrames;

        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _base);
    }

    /// <summary>
    /// The most frames one crossing may carry. Both sides were built for this number, so a
    /// longer block is broken into several rather than being sent whole.
    /// </summary>
    public int MaxFrames { get; }

    /// <summary>The start of the block, for anything that wants to read the header itself.</summary>
    public byte* Base => _base;

    /// <summary>Where the audio going into the plugin is written, interleaved stereo.</summary>
    public float* Input => (float*)(_base + PluginBridge.InputOffset);

    /// <summary>Where what came out of the plugin is read, in the same shape.</summary>
    public float* Output => (float*)(_base + PluginBridge.OutputOffset(MaxFrames));

    /// <summary>
    /// How many events have ever been queued, at offset 16. It counts up and is never wrapped:
    /// the slot is worked out from it, so the difference between the two indexes is how many
    /// are waiting.
    /// </summary>
    private int* WriteIndex => (int*)(_base + 16);

    /// <summary>How many have ever been taken, at offset 20, counted the same way.</summary>
    private int* ReadIndex => (int*)(_base + 20);

    /// <summary>Makes the shared block and says where it is, for the parent to pass on.</summary>
    /// <remarks>
    /// Named after the process and a fresh identifier, so two plugins started at the same
    /// instant cannot land on one another's block.
    /// </remarks>
    /// <param name="maxFrames">The most frames a block will ever carry, which fixes its size.</param>
    /// <param name="path">Where it is, which is what the child is told on its command line.</param>
    public static BridgeBlock Create(int maxFrames, out string path)
    {
        long size = PluginBridge.BlockBytes(maxFrames);

        string folder = Directory.Exists("/dev/shm") ? "/dev/shm" : Path.GetTempPath();

        path = Path.Combine(folder, "jinglebox-plugin-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N") + ".block");

        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);

        stream.SetLength(size);

        var file = MemoryMappedFile.CreateFromFile(
            stream, null, size, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);

        var view = file.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite);

        var block = new BridgeBlock(file, view, path, maxFrames);

        *(int*)block._base = PluginBridge.Magic;
        *(int*)(block._base + 4) = maxFrames;
        *(int*)(block._base + 8) = PluginBridge.Channels;
        *block.WriteIndex = 0;
        *block.ReadIndex = 0;

        return block;
    }

    /// <summary>Opens the block the parent made, or nothing when it is not there.</summary>
    /// <remarks>
    /// Nothing rather than an exception: the parent may have gone away between starting this
    /// process and this process getting far enough to look, and a child that throws on the way
    /// up leaves no account of itself at all.
    ///
    /// That was the promise and only the missing file kept it. Opening the mapping itself threw,
    /// and a plugin's process is exactly where nobody sees a throw: the parent reads an exit code
    /// and says the plugin stopped unexpectedly, which is true and says nothing about why.
    ///
    /// **The file is shared for writing by both sides, and it has to be said out loud.** The
    /// parent holds the same file mapped, and the overload that takes a path opens with sharing
    /// for reading only, so the child's open is a sharing violation on Windows. Nothing on Linux
    /// enforces that, which is why this worked there for as long as Windows never ran it: the
    /// whole platform difference was one default nobody had to think about.
    /// </remarks>
    /// <param name="path">Where the parent said it was.</param>
    public static BridgeBlock? Open(string path)
    {
        if (!File.Exists(path)) return null;

        FileStream stream;

        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }
        catch (Exception bad)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Plugins,
                "the shared block at '" + path + "' would not open", bad);

            return null;
        }

        long length = stream.Length;

        var file = MemoryMappedFile.CreateFromFile(
            stream, null, length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);

        var view = file.CreateViewAccessor(0, length, MemoryMappedFileAccess.ReadWrite);

        byte* start = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref start);

        int magic = *(int*)start;
        int frames = *(int*)(start + 4);

        view.SafeMemoryMappedViewHandle.ReleasePointer();

        if (magic != PluginBridge.Magic || frames <= 0)
        {
            view.Dispose();
            file.Dispose();
            return null;
        }

        return new BridgeBlock(file, view, null, frames);
    }

    /// <summary>
    /// Puts one thing in the queue for the next block.
    /// </summary>
    /// <remarks>
    /// A ring with a writer at one end and a reader at the other, and the two of them in
    /// different processes. The payload is written before the index that publishes it, so the
    /// reader either sees a slot that is finished or does not see it at all. A full ring drops
    /// what will not fit, which for a thousand notes in one block is the right answer.
    /// </remarks>
    public void Queue(BridgeEvent kind, uint id, float value, int extra = 0)
    {
        lock (_writers)
        {
            int write = *WriteIndex;
            int read = System.Threading.Volatile.Read(ref *ReadIndex);

            if (write - read >= PluginBridge.MaxEvents) return;

            byte* slot = _base + PluginBridge.EventsOffset + (write % PluginBridge.MaxEvents) * PluginBridge.EventSize;

            *(int*)slot = (int)kind;
            *(uint*)(slot + 4) = id;
            *(float*)(slot + 8) = value;
            *(int*)(slot + 12) = extra;

            System.Threading.Volatile.Write(ref *WriteIndex, write + 1);
        }
    }

    /// <summary>
    /// Held while a slot is filled in. There is one reader and it is in another process, but
    /// there are several writers here: notes come from the tracker's thread and parameters
    /// from whoever moved one.
    /// </summary>
    private readonly object _writers = new();

    /// <summary>Everything queued since last time, and the queue emptied.</summary>
    /// <remarks>
    /// Read by the other process just before it runs a block, so what is applied is everything
    /// that arrived while the last block was in flight. The count is clamped to the size of the
    /// ring: a writer that has run right round has overwritten what it passed, and reading more
    /// than a ring's worth would be reading the same slots twice.
    /// </remarks>
    public (BridgeEvent Kind, uint Id, float Value, int Extra)[] Take()
    {
        int read = *ReadIndex;
        int write = System.Threading.Volatile.Read(ref *WriteIndex);

        int count = write - read;
        if (count <= 0) return Array.Empty<(BridgeEvent, uint, float, int)>();

        if (count > PluginBridge.MaxEvents) count = PluginBridge.MaxEvents;

        var events = new (BridgeEvent, uint, float, int)[count];

        for (int index = 0; index < count; index++)
        {
            byte* slot = _base + PluginBridge.EventsOffset + ((read + index) % PluginBridge.MaxEvents) * PluginBridge.EventSize;

            events[index] = ((BridgeEvent)(*(int*)slot), *(uint*)(slot + 4), *(float*)(slot + 8), *(int*)(slot + 12));
        }

        System.Threading.Volatile.Write(ref *ReadIndex, write);

        return events;
    }

    /// <summary>
    /// Lets the mapping go, and the file with it for the side that made it.
    /// </summary>
    /// <remarks>
    /// The parent made the file and the parent takes it away. The child only ever opened one,
    /// so it holds no path and deletes nothing, and a file both processes have mapped stays
    /// mapped until they have both let go: deleting it is a name being removed rather than
    /// memory being pulled out from underneath anybody.
    /// </remarks>
    public void Dispose()
    {
        try { _view.SafeMemoryMappedViewHandle.ReleasePointer(); } catch (Exception) { }

        _view.Dispose();
        _file.Dispose();

        if (_path != null)
        {
            try { File.Delete(_path); } catch (Exception) { }
        }
    }
}
