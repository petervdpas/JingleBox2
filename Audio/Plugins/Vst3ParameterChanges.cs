using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The knob moves handed to a plugin at the start of a block.
/// </summary>
/// <remarks>
/// VST3 has no way to tell a plugin a value outside of processing: a move arrives as a list of
/// changes attached to the block, one list per parameter, each holding points in time. This
/// host only ever sends one point, at the start, because a knob dragged by hand is not
/// automation.
///
/// A plugin reads this from the audio thread through vtables, so it is built as native memory
/// with its tables alongside, allocated once and refilled per block. Nothing here allocates
/// while audio is running.
/// </remarks>
internal sealed unsafe class Vst3ParameterChanges : IDisposable
{
    /// <summary>One parameter's worth of change: the id, and the value it moved to.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Queue
    {
        /// <summary>The table, which has to be the first word or the plugin calls rubbish.</summary>
        public nint Vtbl;

        /// <summary>Which parameter, by the plugin's own id.</summary>
        public uint Id;

        /// <summary>Where it moved to, nought to one as every VST3 value is.</summary>
        public double Value;
    }

    /// <summary>The list of them, which is what the plugin is handed.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct List
    {
        /// <summary>The table, which has to be the first word.</summary>
        public nint Vtbl;

        /// <summary>How many parameters moved this block.</summary>
        public int Count;

        /// <summary>How many there is room for. Fixed, since nothing here allocates per block.</summary>
        public int Capacity;

        /// <summary>The queues themselves, allocated once and refilled.</summary>
        public Queue* Queues;
    }

    /// <summary>
    /// The two tables, shared by every instance in the process. A table is code rather than
    /// state, so one of each is enough and neither is ever freed.
    /// </summary>
    private static nint _listTable;

    /// <inheritdoc cref="_listTable"/>
    private static nint _queueTable;

    /// <summary>Held while a table is built, so two instances made at once cannot both build it.</summary>
    private static readonly object Gate = new();

    /// <summary>The unmanaged list. Null once disposed, which every method here checks for.</summary>
    private List* _list;

    /// <summary>
    /// The room, kept alongside the struct's own copy so it can be checked without following a
    /// pointer that may have been freed.
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// Allocates the list and one queue per parameter that can move in a block, up front. Every
    /// queue is given its table here rather than when it is used, since a queue handed to a
    /// plugin with a null first word is a jump through a null pointer inside the plugin.
    /// </summary>
    public Vst3ParameterChanges(int capacity)
    {
        _capacity = Math.Max(1, capacity);

        _list = (List*)NativeMemory.AllocZeroed(1, (nuint)sizeof(List));
        _list->Vtbl = ListTable();
        _list->Capacity = _capacity;
        _list->Queues = (Queue*)NativeMemory.AllocZeroed((nuint)_capacity, (nuint)sizeof(Queue));

        nint table = QueueTable();
        for (int index = 0; index < _capacity; index++) _list->Queues[index].Vtbl = table;
    }

    /// <summary>The pointer that goes into the block.</summary>
    public void* Pointer => _list;

    /// <summary>How many parameters moved this block.</summary>
    public int Count => _list == null ? 0 : _list->Count;

    /// <summary>
    /// Empties it for the next block. The queues are left where they are, tables and all, and
    /// written over.
    /// </summary>
    public void Clear()
    {
        if (_list != null) _list->Count = 0;
    }

    /// <summary>Adds one move. Silently full rather than growing, since this runs per block.</summary>
    public bool Add(uint id, double value)
    {
        if (_list == null || _list->Count >= _capacity) return false;

        _list->Queues[_list->Count].Id = id;
        _list->Queues[_list->Count].Value = value;
        _list->Count++;

        return true;
    }

    /// <summary>
    /// Builds the list's table once: the root's three, then the three IParameterChanges adds, in
    /// the order the header declares them. The order is the whole contract, since a plugin calls
    /// by position and nothing checks.
    /// </summary>
    private static nint ListTable()
    {
        lock (Gate)
        {
            if (_listTable != 0) return _listTable;

            var table = (nint*)NativeMemory.AllocZeroed(6, (nuint)sizeof(nint));

            table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&ListQuery;
            table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, int>)&ListCount;
            table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, int, void*>)&ListAt;
            table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, uint*, int*, void*>)&ListAdd;

            _listTable = (nint)table;
            return _listTable;
        }
    }

    /// <summary>
    /// The same for one parameter's queue: the root's three, then the four IParamValueQueue
    /// adds.
    /// </summary>
    private static nint QueueTable()
    {
        lock (Gate)
        {
            if (_queueTable != 0) return _queueTable;

            var table = (nint*)NativeMemory.AllocZeroed(7, (nuint)sizeof(nint));

            table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&QueueQuery;
            table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&QueueId;
            table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, int>)&QueuePointCount;
            table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, int, int*, double*, int>)&QueuePoint;
            table[6] = (nint)(delegate* unmanaged[Cdecl]<void*, int, double, int*, int>)&QueueAddPoint;

            _queueTable = (nint)table;
            return _queueTable;
        }
    }

    /// <summary>
    /// AddRef and Release for both tables. These objects belong to the host and outlive any call
    /// into a plugin, so neither is ever freed by a count and both always answer one.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint KeepAlive(void* self) => 1;

    /// <summary>
    /// The list is an IParameterChanges and the root interface, and nothing else. Refusing by
    /// name is what stops a plugin calling through a table that does not have the method it
    /// thinks it does.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ListQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.ParameterChangesId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>How many parameters the plugin will be offered this block.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ListCount(void* self)
    {
        var list = (List*)self;
        return list == null ? 0 : list->Count;
    }

    /// <summary>
    /// One parameter's queue, as a pointer into the host's own memory. That is what the
    /// interface asks for here, unlike the event list, which hands back a copy.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void* ListAt(void* self, int index)
    {
        var list = (List*)self;
        if (list == null || index < 0 || index >= list->Count) return null;

        return list->Queues + index;
    }

    /// <summary>
    /// A plugin adding a change of its own, which is what the outgoing list is for. Nothing
    /// reads it back, but it has to be there and it has to answer: some plugins assert on a
    /// host that does not offer one, and an assertion inside a plugin is not a warning.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void* ListAdd(void* self, uint* id, int* index)
    {
        var list = (List*)self;

        if (list == null || list->Count >= list->Capacity)
        {
            if (index != null) *index = -1;
            return null;
        }

        var queue = list->Queues + list->Count;
        queue->Id = id == null ? 0 : *id;
        queue->Value = 0;

        if (index != null) *index = list->Count;
        list->Count++;

        return queue;
    }

    /// <summary>
    /// A queue is an IParamValueQueue and the root interface, and nothing else.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int QueueQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.ParamValueQueueId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>Which parameter this queue is about.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint QueueId(void* self)
    {
        var queue = (Queue*)self;
        return queue == null ? 0 : queue->Id;
    }

    /// <summary>One point, always: the value the knob is at when the block starts.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int QueuePointCount(void* self) => 1;

    /// <summary>
    /// The one point: its offset into the block, which is always nought, and its value. Anything
    /// but index nought is refused rather than clamped, since a plugin asking for a second point
    /// after being told there is one is a plugin that has lost its place.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int QueuePoint(void* self, int index, int* offset, double* value)
    {
        var queue = (Queue*)self;
        if (queue == null || index != 0) return Vst3Abi.NoInterface;

        if (offset != null) *offset = 0;
        if (value != null) *value = queue->Value;

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// A plugin writing a point into a queue of its own, which is what the outgoing list is for.
    /// The value is kept rather than dropped so that reading it back gives what was written,
    /// which is what a plugin that checks its own work expects. The offset is ignored: there is
    /// only ever the one point, at the start of the block.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int QueueAddPoint(void* self, int offset, double value, int* index)
    {
        var queue = (Queue*)self;
        if (queue == null) return Vst3Abi.NoInterface;

        queue->Value = value;

        if (index != null) *index = 0;
        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Frees the queues and the list. Called with no audio running: a plugin holding one of
    /// these pointers through a block would be reading freed memory.
    /// </summary>
    public void Dispose()
    {
        if (_list == null) return;

        NativeMemory.Free(_list->Queues);
        NativeMemory.Free(_list);

        _list = null;
    }
}
