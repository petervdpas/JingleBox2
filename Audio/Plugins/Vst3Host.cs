using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The pieces of a host a VST3 plugin expects to be handed: something to ask the host's name,
/// something to report knob moves to, and somewhere to put a lump of state.
/// </summary>
/// <remarks>
/// A plugin reaches these through vtables, so they are built as native objects rather than
/// managed ones: a block of memory whose first word points at a table of function pointers.
/// Everything they need lives in that same block, which is what makes them safe to call from
/// whatever thread a plugin feels like calling them on.
///
/// None of these are optional in practice. Serum refuses to start without a host context, and
/// a controller handed no handler is a controller that may decide the host is broken.
/// </remarks>
internal static unsafe class Vst3Host
{
    /// <summary>What a plugin is told it is running inside.</summary>
    public const string HostName = "JingleBox2";

    /// <summary>
    /// The host context, made on first use and never freed. One for the process, since it holds
    /// nothing about any particular plugin.
    /// </summary>
    private static void* _application;

    /// <summary>Held while it is built, so two plugins loading at once cannot both build it.</summary>
    private static readonly object Gate = new();

    /// <summary>
    /// The one host context, shared by every plugin. Built once and never freed, because a
    /// plugin may hold on to it for as long as it lives.
    /// </summary>
    /// <remarks>
    /// The table is the root's three, then IHostApplication's two, in the order the header
    /// declares them. The order is the whole contract, since a plugin calls by position and
    /// nothing checks.
    /// </remarks>
    public static void* Application()
    {
        lock (Gate)
        {
            if (_application != null) return _application;

            var table = (nint*)NativeMemory.AllocZeroed(5, (nuint)sizeof(nint));

            table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&ApplicationQuery;
            table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, char*, int>)&ApplicationName;
            table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, byte*, void**, int>)&ApplicationCreate;

            var host = (nint*)NativeMemory.AllocZeroed(1, (nuint)sizeof(nint));
            host[0] = (nint)table;

            _application = host;
            return _application;
        }
    }

    /// <summary>
    /// The host context is an IHostApplication and the root interface, and on X11 it is also
    /// where the run loop is handed over: that is where a plugin looks for it, and on X11 it is
    /// not optional. See <see cref="PluginRunLoop"/>.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ApplicationQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.HostApplicationId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && Vst3Abi.SameId(id, Vst3Abi.RunLoopId))
        {
            *result = PluginRunLoop.Instance();
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>
    /// Reference counting for objects the host owns for the life of the process. Answering one
    /// rather than zero keeps a plugin from concluding the object has already gone.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint KeepAlive(void* self) => 1;

    /// <summary>
    /// What the plugin is told it is running inside. The buffer is a String128, which is 128
    /// UTF-16 characters with the terminator among them, so 127 is the most that can be written.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ApplicationName(void* self, char* name)
    {
        if (name == null) return Vst3Abi.NoInterface;

        for (int index = 0; index < HostName.Length && index < 127; index++) name[index] = HostName[index];
        name[Math.Min(HostName.Length, 127)] = '\0';

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Making host objects on a plugin's behalf: a message, or the list of things written on it.
    /// </summary>
    /// <remarks>
    /// This is where a plugin asks for an envelope so its two halves can post to each other.
    /// Refusing is not the harmless answer it looks like. The plugin gets nothing back and most
    /// of them do not check, because no real host refuses, and the ones that do not check die
    /// on the next line. See <see cref="Vst3Messages"/>.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ApplicationCreate(void* self, byte* cid, byte* id, void** result) =>
        Vst3Messages.Create(cid, id, result);

    /// <summary>
    /// What a controller reports knob moves to. One per plugin, since a plugin holds it.
    /// </summary>
    /// <remarks>
    /// This is the only way a knob moved in a plugin's own window reaches anything. VST3 keeps
    /// the half that draws and the half that plays apart on purpose, and neither tells the
    /// other: the drawing half reports the move here, and the host is expected to hand it to
    /// the playing half on its next block. A host that ignores this has plugin windows whose
    /// knobs turn and change nothing.
    ///
    /// The object carries a number rather than a pointer back to the plugin, because it is
    /// native memory a plugin holds for as long as it likes and a managed object cannot be
    /// left lying in it.
    /// </remarks>
    public static void* CreateHandler(int slot)
    {
        var table = (nint*)NativeMemory.AllocZeroed(7, (nuint)sizeof(nint));

        table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&HandlerQuery;
        table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
        table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
        table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, uint, int>)&HandlerBeginEdit;
        table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, uint, double, int>)&HandlerPerformEdit;
        table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, uint, int>)&HandlerEndEdit;
        table[6] = (nint)(delegate* unmanaged[Cdecl]<void*, int, int>)&HandlerRestart;

        var handler = (nint*)NativeMemory.AllocZeroed(2, (nuint)sizeof(nint));
        handler[0] = (nint)table;
        handler[1] = slot;

        return handler;
    }

    /// <summary>
    /// Where each plugin's knob moves go, by slot number. A number rather than a pointer,
    /// because a handler is native memory a plugin holds for as long as it likes and a managed
    /// object cannot be left lying in it.
    /// </summary>
    private static readonly Dictionary<int, Action<uint, double>> Moves = new();

    /// <summary>Held over every read and write of the two dictionaries and the counter.</summary>
    private static readonly object MoveGate = new();

    /// <summary>
    /// The last slot given out. Never reused, so a callback arriving late from a plugin that has
    /// already gone finds nothing rather than finding somebody else's.
    /// </summary>
    private static int _slots;

    /// <summary>A number for a plugin about to be loaded, to find it again from a callback.</summary>
    public static int NextSlot()
    {
        lock (MoveGate) return ++_slots;
    }

    /// <summary>Says where a slot's knob moves should go.</summary>
    public static void Listen(int slot, Action<uint, double> moved)
    {
        lock (MoveGate) Moves[slot] = moved;
    }

    /// <summary>Stops listening, for a plugin going away.</summary>
    public static void Forget(int slot)
    {
        lock (MoveGate)
        {
            Moves.Remove(slot);
            Reloaded.Remove(slot);
        }
    }

    /// <summary>
    /// Whose knob moves a handler is about, read out of the second word of the handler block.
    /// Null for a plugin that has already been forgotten, which is what a callback arriving
    /// during teardown looks like.
    /// </summary>
    private static Action<uint, double>? Whose(void* self)
    {
        if (self == null) return null;

        int slot = (int)((nint*)self)[1];

        lock (MoveGate) return Moves.TryGetValue(slot, out var moved) ? moved : null;
    }

    /// <summary>A handler is an IComponentHandler and the root interface, and nothing else.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.ComponentHandlerId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>
    /// The plugin saying somebody has taken hold of a knob. Accepted and ignored: it marks the
    /// start of a gesture, which matters to a host that writes automation from one and not to
    /// this one, where a move is a move.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerBeginEdit(void* self, uint id) => Vst3Abi.ResultOk;

    /// <summary>
    /// The plugin's own window reporting a knob. Passed on to the plugin, which queues it for
    /// the half that plays and tells the host it has something worth saving.
    /// </summary>
    /// <remarks>
    /// Arrives on whatever thread the plugin's window is on, and anything that throws is
    /// swallowed: a knob move is not worth throwing back into somebody else's toolkit, which
    /// would unwind through C++ frames that were not built for it.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerPerformEdit(void* self, uint id, double value)
    {
        try
        {
            Whose(self)?.Invoke(id, value);
        }
        catch (Exception)
        {
        }

        return Vst3Abi.ResultOk;
    }

    /// <inheritdoc cref="HandlerBeginEdit"/>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerEndEdit(void* self, uint id) => Vst3Abi.ResultOk;

    /// <summary>
    /// The plugin asking to be looked at again, which is what loading a preset in its own
    /// window comes through as. Nothing is restarted here: what matters to the host is that
    /// the sound is not the sound it was, so there is something to save.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerRestart(void* self, int flags)
    {
        try
        {
            Reloads(self)?.Invoke();
        }
        catch (Exception)
        {
        }

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Where each plugin's "everything changed" goes, by the same slot number as
    /// <see cref="Moves"/>. Two dictionaries rather than one entry with two fields, so a plugin
    /// that only ever does one of the two costs nothing for the other.
    /// </summary>
    private static readonly Dictionary<int, Action> Reloaded = new();

    /// <summary>Says where a slot's "everything changed" should go.</summary>
    public static void ListenForReload(int slot, Action reloaded)
    {
        lock (MoveGate) Reloaded[slot] = reloaded;
    }

    /// <inheritdoc cref="Whose"/>
    private static Action? Reloads(void* self)
    {
        if (self == null) return null;

        int slot = (int)((nint*)self)[1];

        lock (MoveGate) return Reloaded.TryGetValue(slot, out var reloaded) ? reloaded : null;
    }
}

/// <summary>
/// A lump of bytes a plugin can read and write, which is how the two halves of a plugin agree
/// on what the settings are.
/// </summary>
/// <remarks>
/// The audio half is asked for its state and the settings half is handed it, so the knobs read
/// what the sound is actually doing. Both halves get a pointer to this object, so like the
/// other host objects it lives in native memory with its own vtable, and its buffer grows as
/// it is written to.
/// </remarks>
internal sealed unsafe class Vst3Stream : IDisposable
{
    /// <summary>
    /// Enough for the many plugins whose state is a few hundred bytes, and doubled from there
    /// for the ones whose patch is a third of a megabyte.
    /// </summary>
    private const int InitialCapacity = 4096;

    /// <summary>What the plugin is handed: an IBStream, and the buffer behind it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Body
    {
        /// <summary>The table, which has to be the first word or the plugin calls rubbish.</summary>
        public nint Vtbl;

        /// <summary>The buffer. Replaced when it grows, so nothing may hold a pointer into it.</summary>
        public byte* Data;

        /// <summary>How much has been written, which is what a read runs out at.</summary>
        public long Length;

        /// <summary>How much room there is. Doubled rather than grown by the amount wanted.</summary>
        public long Capacity;

        /// <summary>Where the next read or write goes. The plugin moves it by seeking.</summary>
        public long Position;
    }

    /// <summary>
    /// The one table, shared by every stream in the process. A table is code rather than state,
    /// so one is enough and it is never freed.
    /// </summary>
    private static nint _table;

    /// <summary>Held while the table is built, so two streams made at once cannot both build it.</summary>
    private static readonly object Gate = new();

    /// <summary>The unmanaged stream. Null once disposed, which every method here checks for.</summary>
    private Body* _body;

    /// <summary>Makes an empty stream, ready to be written to or filled.</summary>
    public Vst3Stream()
    {
        _body = (Body*)NativeMemory.AllocZeroed(1, (nuint)sizeof(Body));
        _body->Vtbl = Table();
        _body->Data = (byte*)NativeMemory.AllocZeroed(InitialCapacity, 1);
        _body->Capacity = InitialCapacity;
    }

    /// <summary>The pointer a plugin is handed.</summary>
    public void* Pointer => _body;

    /// <summary>Everything written so far, copied out.</summary>
    public byte[] ToArray()
    {
        if (_body == null || _body->Length <= 0) return Array.Empty<byte>();

        var copy = new byte[_body->Length];
        Marshal.Copy((nint)_body->Data, copy, 0, copy.Length);

        return copy;
    }

    /// <summary>Fills the stream from a saved lump, ready to be read from the start.</summary>
    public void Fill(byte[] bytes)
    {
        if (_body == null || bytes == null || bytes.Length == 0) return;

        Reserve(_body, bytes.Length);
        Marshal.Copy(bytes, 0, (nint)_body->Data, bytes.Length);

        _body->Length = bytes.Length;
        _body->Position = 0;
    }

    /// <summary>Rewinds, which is what has to happen between the write and the read.</summary>
    public void Rewind()
    {
        if (_body != null) _body->Position = 0;
    }

    /// <summary>How many bytes have been written, whatever the position is.</summary>
    public long Length => _body == null ? 0 : _body->Length;

    /// <summary>
    /// True when there is nothing in here worth passing on. Empty is the obvious case; a lone
    /// nought is the other one, which is what a plugin with no state of its own writes, and
    /// handing that back to the plugin is what makes some of them assert on a read that
    /// returns nothing.
    /// </summary>
    public bool LooksEmpty
    {
        get
        {
            if (_body == null || _body->Length == 0) return true;

            return _body->Length == 1 && _body->Data[0] == 0;
        }
    }

    /// <summary>
    /// Builds the shared table once: the root's three, then IBStream's four, in the order the
    /// header declares them.
    /// </summary>
    private static nint Table()
    {
        lock (Gate)
        {
            if (_table != 0) return _table;

            var table = (nint*)NativeMemory.AllocZeroed(7, (nuint)sizeof(nint));

            table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&Query;
            table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&AddReference;
            table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&AddReference;
            table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, void*, int, int*, int>)&Read;
            table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, void*, int, int*, int>)&Write;
            table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, long, int, long*, int>)&Seek;
            table[6] = (nint)(delegate* unmanaged[Cdecl]<void*, long*, int>)&Tell;

            _table = (nint)table;
            return _table;
        }
    }

    /// <summary>A stream is an IBStream and the root interface, and nothing else.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Query(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.BStreamId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>
    /// AddRef and Release both. A stream is freed by the host that made it rather than by a
    /// count, so it always answers one: nought would tell a plugin the object had already gone.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint AddReference(void* self) => 1;

    /// <summary>
    /// Reads up to that many bytes and says how many it really gave. Fewer than asked for, and
    /// nought at the end, are both success: that is how a plugin knows to stop asking.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Read(void* self, void* buffer, int count, int* read)
    {
        var body = (Body*)self;
        if (body == null || count < 0) return Vst3Abi.NoInterface;

        long available = body->Length - body->Position;
        int taken = (int)Math.Max(0, Math.Min(count, available));

        if (taken > 0 && buffer != null)
        {
            Unsafe.CopyBlock(buffer, body->Data + body->Position, (uint)taken);
            body->Position += taken;
        }

        if (read != null) *read = taken;
        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Takes that many bytes, growing the buffer as needed, and always says it took all of them.
    /// A short write would have a plugin come round again, and there is nothing here that can
    /// run out except memory.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Write(void* self, void* buffer, int count, int* written)
    {
        var body = (Body*)self;
        if (body == null || count < 0) return Vst3Abi.NoInterface;

        if (count > 0 && buffer != null)
        {
            Reserve(body, body->Position + count);

            Unsafe.CopyBlock(body->Data + body->Position, buffer, (uint)count);
            body->Position += count;

            if (body->Position > body->Length) body->Length = body->Position;
        }

        if (written != null) *written = count;
        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Makes room for at least that many bytes, doubling rather than growing by the amount
    /// wanted: a plugin writing its patch in small pieces would otherwise copy the whole thing
    /// once per piece. The old buffer is freed, so nothing may hold a pointer into it across a
    /// write.
    /// </summary>
    private static void Reserve(Body* body, long wanted)
    {
        if (wanted <= body->Capacity) return;

        long capacity = body->Capacity;
        while (capacity < wanted) capacity *= 2;

        var grown = (byte*)NativeMemory.AllocZeroed((nuint)capacity, 1);

        if (body->Length > 0) Unsafe.CopyBlock(grown, body->Data, (uint)body->Length);

        NativeMemory.Free(body->Data);

        body->Data = grown;
        body->Capacity = capacity;
    }

    /// <summary>
    /// Moves the position. The mode is the ABI's own: 1 is from where we are, 2 is from the end,
    /// and anything else is from the start. Clamped rather than refused, since a plugin seeking
    /// past the end and then reading is asking for nothing and should be given nothing.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Seek(void* self, long position, int mode, long* result)
    {
        var body = (Body*)self;
        if (body == null) return Vst3Abi.NoInterface;

        long target = mode switch
        {
            1 => body->Position + position,
            2 => body->Length + position,
            _ => position
        };

        body->Position = Math.Clamp(target, 0, body->Length);

        if (result != null) *result = body->Position;
        return Vst3Abi.ResultOk;
    }

    /// <summary>Where the position is.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Tell(void* self, long* position)
    {
        var body = (Body*)self;
        if (body == null) return Vst3Abi.NoInterface;

        if (position != null) *position = body->Position;
        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Frees the buffer and the stream. Called once both halves of the plugin are done with it:
    /// a plugin holding this pointer afterwards would be reading freed memory.
    /// </summary>
    public void Dispose()
    {
        if (_body == null) return;

        NativeMemory.Free(_body->Data);
        NativeMemory.Free(_body);

        _body = null;
    }
}
