using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

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
    public const int Magic = 0x4A424147;

    /// <summary>Every plugin here is stereo in and stereo out.</summary>
    public const int Channels = 2;

    /// <summary>Parameter moves and notes waiting for the next block. Beyond this they are dropped.</summary>
    public const int MaxEvents = 1024;

    /// <summary>One event: what kind, which parameter or note, and how much.</summary>
    public const int EventSize = 16;

    /// <summary>Where the events start, past the header.</summary>
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

    /// <summary>How big the shared block has to be for this many frames.</summary>
    public static long BlockBytes(int maxFrames) =>
        AudioOffset + (long)Math.Max(1, maxFrames) * Channels * sizeof(float) * 2;

    /// <summary>Where the input audio sits inside the block.</summary>
    public const int InputOffset = AudioOffset;

    /// <summary>Where the output audio sits inside the block.</summary>
    public static int OutputOffset(int maxFrames) => AudioOffset + maxFrames * Channels * sizeof(float);
}

/// <summary>What one message is about. Both processes read from this same list.</summary>
internal enum BridgeCall : byte
{
    None = 0,

    /// <summary>Child to parent, once, when the plugin is loaded and the answer is known.</summary>
    Hello = 1,

    /// <summary>Everything the plugin exposes, sent once at the start.</summary>
    Parameters = 2,

    /// <summary>Parent to child: move a parameter.</summary>
    SetValue = 3,

    /// <summary>Parent to child: what is this set to now.</summary>
    ValueOf = 4,

    /// <summary>Parent to child: how does the plugin word this value.</summary>
    TextFor = 5,

    /// <summary>Parent to child: hand over anything queued now rather than on the next block.</summary>
    Flush = 6,

    /// <summary>Parent to child: everything inside the plugin, as a lump.</summary>
    SaveState = 7,

    /// <summary>Parent to child: put a lump back.</summary>
    LoadState = 8,

    /// <summary>Parent to child: open the plugin's own interface.</summary>
    OpenEditor = 9,

    /// <summary>Parent to child: put the interface inside this window.</summary>
    Attach = 10,

    /// <summary>Parent to child: take it back out.</summary>
    Detach = 11,

    /// <summary>Parent to child: the window is now this size.</summary>
    Resized = 12,

    /// <summary>Parent to child: put the interface away.</summary>
    CloseEditor = 13,

    /// <summary>Child to parent, unasked: the plugin wants a different size.</summary>
    ResizeRequested = 14,

    /// <summary>Parent to child: stop.</summary>
    Quit = 15,

    /// <summary>The answer to anything that only needs a yes.</summary>
    Ok = 16,

    /// <summary>The answer to anything that went wrong, with a reason.</summary>
    Fail = 17,

    /// <summary>The answer to a question whose answer is a number.</summary>
    Value = 18,

    /// <summary>The answer to a question whose answer is words.</summary>
    Text = 19,

    /// <summary>The answer to a question whose answer is a lump of bytes.</summary>
    State = 20,

    /// <summary>Parent to child on the audio socket: there is a block waiting.</summary>
    Process = 21,

    /// <summary>Child to parent on the audio socket: the block is done.</summary>
    Rendered = 22,

    /// <summary>Child to parent, unasked: something worth putting in the log.</summary>
    Note = 23,

    /// <summary>Child to parent, unasked: the plugin moved one of its own knobs.</summary>
    Edited = 24,

    /// <summary>Child to parent, unasked: everything about the plugin may have changed at once.</summary>
    Reloaded = 25
}

/// <summary>What one queued event is. Written into the shared block before a block is asked for.</summary>
internal enum BridgeEvent : int
{
    None = 0,
    ParameterValue = 1,
    NoteOn = 2,
    NoteOff = 3,
    AllNotesOff = 4
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
    private readonly Socket _socket;
    private readonly object _write = new();
    private readonly byte[] _header = new byte[5];

    public BridgeLink(Socket socket) => _socket = socket;

    public Socket Socket => _socket;

    /// <summary>True while the other end is still there.</summary>
    public bool Connected => _socket.Connected;

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

    public void Dispose()
    {
        try { _socket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
        try { _socket.Dispose(); } catch (Exception) { }
    }
}

/// <summary>Builds and reads the bodies of messages, so both sides agree on the order of things.</summary>
internal static class BridgeBody
{
    public static byte[] Words(params string[] words)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        writer.Write(words.Length);
        foreach (var word in words) writer.Write(word ?? "");

        return stream.ToArray();
    }

    public static string[] ReadWords(byte[] payload)
    {
        using var stream = new MemoryStream(payload);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        int count = reader.ReadInt32();
        var words = new string[Math.Max(0, count)];

        for (int index = 0; index < words.Length; index++) words[index] = reader.ReadString();

        return words;
    }

    public static byte[] Number(uint id, double value)
    {
        var payload = new byte[12];

        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), id);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 8), value);

        return payload;
    }

    public static (uint Id, double Value) ReadNumber(byte[] payload) =>
        payload.Length < 12
            ? (0u, 0d)
            : (BitConverter.ToUInt32(payload, 0), BitConverter.ToDouble(payload, 4));

    public static byte[] Double(double value) => BitConverter.GetBytes(value);

    public static double ReadDouble(byte[] payload) =>
        payload.Length < 8 ? 0d : BitConverter.ToDouble(payload, 0);

    public static byte[] Pair(int first, int second)
    {
        var payload = new byte[8];

        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), first);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 4), second);

        return payload;
    }

    public static (int First, int Second) ReadPair(byte[] payload) =>
        payload.Length < 8 ? (0, 0) : (BitConverter.ToInt32(payload, 0), BitConverter.ToInt32(payload, 4));

    public static byte[] Three(int first, int second, int third)
    {
        var payload = new byte[12];

        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), first);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 4), second);
        BitConverter.TryWriteBytes(payload.AsSpan(8, 4), third);

        return payload;
    }

    public static (int First, int Second, int Third) ReadThree(byte[] payload) =>
        payload.Length < 12
            ? (0, 0, 0)
            : (BitConverter.ToInt32(payload, 0), BitConverter.ToInt32(payload, 4), BitConverter.ToInt32(payload, 8));

    public static byte[] Handle(nint window) => BitConverter.GetBytes((long)window);

    public static nint ReadHandle(byte[] payload) =>
        payload.Length < 8 ? 0 : (nint)BitConverter.ToInt64(payload, 0);

    /// <summary>Every parameter the plugin has, in the order it lists them.</summary>
    public static byte[] Parameters(System.Collections.Generic.IReadOnlyList<PluginParameter> parameters)
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

    public static PluginParameter[] ReadParameters(byte[] payload)
    {
        using var stream = new MemoryStream(payload);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        int count = reader.ReadInt32();
        if (count < 0) count = 0;

        var parameters = new PluginParameter[count];

        for (int index = 0; index < count; index++)
        {
            parameters[index] = new PluginParameter(
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
                reader.ReadString());
        }

        return parameters;
    }
}

/// <summary>
/// The block of memory both processes can see: the audio, and whatever is queued to go with it.
/// </summary>
/// <remarks>
/// A file rather than a name, because a named shared mapping is a Windows idea and this has to
/// work on Linux first. The file lives where the system keeps things that are not really files,
/// so it never touches a disk.
/// </remarks>
internal sealed unsafe class BridgeBlock : IDisposable
{
    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _view;
    private readonly string? _path;
    private byte* _base;

    private BridgeBlock(MemoryMappedFile file, MemoryMappedViewAccessor view, string? path, int maxFrames)
    {
        _file = file;
        _view = view;
        _path = path;

        MaxFrames = maxFrames;

        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _base);
    }

    public int MaxFrames { get; }

    public byte* Base => _base;

    public float* Input => (float*)(_base + PluginBridge.InputOffset);

    public float* Output => (float*)(_base + PluginBridge.OutputOffset(MaxFrames));

    private int* WriteIndex => (int*)(_base + 16);

    private int* ReadIndex => (int*)(_base + 20);

    /// <summary>Makes the block and the file behind it. The parent does this.</summary>
    public static BridgeBlock Create(int maxFrames, out string path)
    {
        long size = PluginBridge.BlockBytes(maxFrames);

        string folder = Directory.Exists("/dev/shm") ? "/dev/shm" : Path.GetTempPath();

        path = Path.Combine(folder, "jinglebox-plugin-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N") + ".block");

        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            stream.SetLength(size);
        }

        var file = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, size, MemoryMappedFileAccess.ReadWrite);
        var view = file.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite);

        var block = new BridgeBlock(file, view, path, maxFrames);

        *(int*)block._base = PluginBridge.Magic;
        *(int*)(block._base + 4) = maxFrames;
        *(int*)(block._base + 8) = PluginBridge.Channels;
        *block.WriteIndex = 0;
        *block.ReadIndex = 0;

        return block;
    }

    /// <summary>Opens a block somebody else made. The child does this.</summary>
    public static BridgeBlock? Open(string path)
    {
        if (!File.Exists(path)) return null;

        var info = new FileInfo(path);

        var file = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, info.Length, MemoryMappedFileAccess.ReadWrite);
        var view = file.CreateViewAccessor(0, info.Length, MemoryMappedFileAccess.ReadWrite);

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

    private readonly object _writers = new();

    /// <summary>Everything queued since last time, and the queue emptied.</summary>
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

    public void Dispose()
    {
        try { _view.SafeMemoryMappedViewHandle.ReleasePointer(); } catch (Exception) { }

        _view.Dispose();
        _file.Dispose();

        // The parent made the file and the parent takes it away. The child only ever opened it,
        // and a file both processes have mapped stays mapped until they have both let go.
        if (_path != null)
        {
            try { File.Delete(_path); } catch (Exception) { }
        }
    }
}
