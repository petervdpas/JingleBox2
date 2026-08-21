using System;
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

    private static void* _application;
    private static readonly object Gate = new();

    /// <summary>
    /// The one host context, shared by every plugin. Built once and never freed, because a
    /// plugin may hold on to it for as long as it lives.
    /// </summary>
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ApplicationQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.HostApplicationId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        // The run loop is a separate object, handed out from here because the host context is
        // where a plugin looks for it. On X11 this is not optional: see Vst3RunLoop.
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && Vst3Abi.SameId(id, Vst3Abi.RunLoopId))
        {
            *result = Vst3RunLoop.Instance();
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ApplicationName(void* self, char* name)
    {
        if (name == null) return Vst3Abi.NoInterface;

        // String128 is 128 UTF-16 characters, terminator included.
        for (int index = 0; index < HostName.Length && index < 127; index++) name[index] = HostName[index];
        name[Math.Min(HostName.Length, 127)] = '\0';

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Making host objects on a plugin's behalf, which for now means saying no.
    /// </summary>
    /// <remarks>
    /// This is where a plugin asks for a message or an attribute list so its two halves can
    /// talk. Refusing costs a plugin whatever it was going to say to itself; every plugin
    /// tested here carries on without it. Worth revisiting the day one does not.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ApplicationCreate(void* self, byte* cid, byte* id, void** result)
    {
        if (result != null) *result = null;
        return Vst3Abi.NotImplemented;
    }

    /// <summary>
    /// What a controller reports knob moves to. One per plugin, since a plugin holds it.
    /// </summary>
    /// <remarks>
    /// Nothing is done with the moves yet: without the plugin's own window there is nothing to
    /// move a knob. The object still has to exist and answer, which is the whole job for now.
    /// </remarks>
    public static void* CreateHandler()
    {
        var table = (nint*)NativeMemory.AllocZeroed(7, (nuint)sizeof(nint));

        table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&HandlerQuery;
        table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
        table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
        table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, uint, int>)&HandlerBeginEdit;
        table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, uint, double, int>)&HandlerPerformEdit;
        table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, uint, int>)&HandlerEndEdit;
        table[6] = (nint)(delegate* unmanaged[Cdecl]<void*, int, int>)&HandlerRestart;

        var handler = (nint*)NativeMemory.AllocZeroed(1, (nuint)sizeof(nint));
        handler[0] = (nint)table;

        return handler;
    }

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerBeginEdit(void* self, uint id) => Vst3Abi.ResultOk;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerPerformEdit(void* self, uint id, double value) => Vst3Abi.ResultOk;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerEndEdit(void* self, uint id) => Vst3Abi.ResultOk;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HandlerRestart(void* self, int flags) => Vst3Abi.ResultOk;
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
    private const int InitialCapacity = 4096;

    [StructLayout(LayoutKind.Sequential)]
    private struct Body
    {
        public nint Vtbl;
        public byte* Data;
        public long Length;
        public long Capacity;
        public long Position;
    }

    private static nint _table;
    private static readonly object Gate = new();

    private Body* _body;

    public Vst3Stream()
    {
        _body = (Body*)NativeMemory.AllocZeroed(1, (nuint)sizeof(Body));
        _body->Vtbl = Table();
        _body->Data = (byte*)NativeMemory.AllocZeroed(InitialCapacity, 1);
        _body->Capacity = InitialCapacity;
    }

    /// <summary>The pointer a plugin is handed.</summary>
    public void* Pointer => _body;

    /// <summary>Rewinds, which is what has to happen between the write and the read.</summary>
    public void Rewind()
    {
        if (_body != null) _body->Position = 0;
    }

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint AddReference(void* self) => 1;

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Tell(void* self, long* position)
    {
        var body = (Body*)self;
        if (body == null) return Vst3Abi.NoInterface;

        if (position != null) *position = body->Position;
        return Vst3Abi.ResultOk;
    }

    public void Dispose()
    {
        if (_body == null) return;

        NativeMemory.Free(_body->Data);
        NativeMemory.Free(_body);

        _body = null;
    }
}
