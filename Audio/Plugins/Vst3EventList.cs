using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The notes handed to a plugin at the start of a block.
/// </summary>
/// <remarks>
/// An instrument is told about notes the same way an effect is told about knobs: as a list
/// attached to the block, read on the audio thread. So this is built the same way as
/// <see cref="Vst3ParameterChanges"/>, as native memory with its table alongside, filled once
/// per block and never grown while audio is running.
/// </remarks>
internal sealed unsafe class Vst3EventList : IDisposable
{
    /// <summary>
    /// What the plugin is actually handed: an IEventList, which to a C++ compiler is an object
    /// whose first word points at a table of function pointers. Everything after that word is
    /// this host's own and the plugin never sees it.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct List
    {
        /// <summary>The table, which has to be the first word or the plugin calls rubbish.</summary>
        public nint Vtbl;

        /// <summary>How many events are in it this block.</summary>
        public int Count;

        /// <summary>How many there is room for. Fixed, since nothing here allocates per block.</summary>
        public int Capacity;

        /// <summary>The events themselves, allocated once and refilled.</summary>
        public Vst3Event* Events;
    }

    /// <summary>
    /// The one table, shared by every list in the process. A table is code rather than state, so
    /// one is enough and it is never freed.
    /// </summary>
    private static nint _table;

    /// <summary>Held while the table is built, so two lists made at once cannot both build it.</summary>
    private static readonly object Gate = new();

    /// <summary>The unmanaged list. Null once disposed, which every method here checks for.</summary>
    private List* _list;

    /// <summary>
    /// The room, kept alongside the struct's own copy so it can be checked without following a
    /// pointer that may have been freed.
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// Allocates the list and its events up front, for the most notes one block will ever carry.
    /// At least one, since a list with no room at all could never take a note.
    /// </summary>
    public Vst3EventList(int capacity)
    {
        _capacity = Math.Max(1, capacity);

        _list = (List*)NativeMemory.AllocZeroed(1, (nuint)sizeof(List));
        _list->Vtbl = Table();
        _list->Capacity = _capacity;
        _list->Events = (Vst3Event*)NativeMemory.AllocZeroed((nuint)_capacity, (nuint)sizeof(Vst3Event));
    }

    /// <summary>The pointer that goes into the block, as the plugin's event list.</summary>
    public void* Pointer => _list;

    /// <summary>How many notes are in this block.</summary>
    public int Count => _list == null ? 0 : _list->Count;

    /// <summary>
    /// Empties it for the next block. The events themselves are left where they are and written
    /// over, since clearing memory nobody will read costs a block's worth of work for nothing.
    /// </summary>
    public void Clear()
    {
        if (_list != null) _list->Count = 0;
    }

    /// <summary>
    /// A note starting. Full velocity is one, and the offset is where in the block it happens.
    /// </summary>
    public bool NoteOn(int pitch, float velocity, int noteId, int channel = 0, int offset = 0)
    {
        var slot = Next();
        if (slot == null) return false;

        slot->Type = Vst3Abi.NoteOnEvent;
        slot->SampleOffset = Math.Max(0, offset);

        slot->OnChannel = (short)channel;
        slot->OnPitch = (short)Math.Clamp(pitch, 0, 127);
        slot->OnTuning = 0;
        slot->OnVelocity = Math.Clamp(velocity, 0, 1);
        slot->OnLength = 0;
        slot->OnNoteId = noteId;

        return true;
    }

    /// <summary>
    /// A note ending. The identifier is what ties it to the note that started, which is what
    /// lets the same pitch sound twice at once without the first ending the second.
    /// </summary>
    public bool NoteOff(int pitch, float velocity, int noteId, int channel = 0, int offset = 0)
    {
        var slot = Next();
        if (slot == null) return false;

        slot->Type = Vst3Abi.NoteOffEvent;
        slot->SampleOffset = Math.Max(0, offset);

        slot->OffChannel = (short)channel;
        slot->OffPitch = (short)Math.Clamp(pitch, 0, 127);
        slot->OffVelocity = Math.Clamp(velocity, 0, 1);
        slot->OffNoteId = noteId;
        slot->OffTuning = 0;

        return true;
    }

    /// <summary>
    /// Takes the next free slot, or null when the block is full. The fields that are shared
    /// between a note on and a note off are cleared here rather than in the two callers, since
    /// everything not written stays as whatever note used this slot last block: a stale bus
    /// index or position would be inherited silently.
    /// </summary>
    private Vst3Event* Next()
    {
        if (_list == null || _list->Count >= _capacity) return null;

        var slot = _list->Events + _list->Count;
        _list->Count++;

        slot->BusIndex = 0;
        slot->PpqPosition = 0;
        slot->Flags = Vst3Abi.LiveEvent;

        return slot;
    }

    /// <summary>
    /// Builds the shared table once: the root's three, then the three IEventList adds, in the
    /// order the header declares them. The order is the whole contract, since a plugin calls by
    /// position and nothing checks.
    /// </summary>
    private static nint Table()
    {
        lock (Gate)
        {
            if (_table != 0) return _table;

            var table = (nint*)NativeMemory.AllocZeroed(6, (nuint)sizeof(nint));

            table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&Query;
            table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, int>)&EventCount;
            table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, int, Vst3Event*, int>)&EventAt;
            table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, Vst3Event*, int>)&AddEvent;

            _table = (nint)table;
            return _table;
        }
    }

    /// <summary>
    /// AddRef and Release both. This list belongs to the host and outlives any call into a
    /// plugin, so it is never freed by a count and always answers one.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint KeepAlive(void* self) => 1;

    /// <summary>
    /// This object is an event list and the root interface, and nothing else. Refusing by name
    /// rather than handing back the same pointer for everything is what stops a plugin calling
    /// through a table that does not have the method it thinks it does.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Query(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.EventListId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>How many events the plugin will be offered this block.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int EventCount(void* self)
    {
        var list = (List*)self;
        return list == null ? 0 : list->Count;
    }

    /// <summary>
    /// One event, handed back by value: the plugin is given a copy rather than a pointer into
    /// the host's own memory, which is what the interface asks for and what keeps a plugin that
    /// holds on to it from reading a slot the next block has rewritten.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int EventAt(void* self, int index, Vst3Event* into)
    {
        var list = (List*)self;
        if (list == null || into == null || index < 0 || index >= list->Count) return Vst3Abi.NoInterface;

        *into = list->Events[index];
        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// A plugin sending an event of its own, which only happens on the outgoing list. Taken
    /// and dropped, because nothing here listens to a plugin's notes yet.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AddEvent(void* self, Vst3Event* added) => Vst3Abi.ResultOk;

    /// <summary>
    /// Frees the events and the list. Called with no audio running: a plugin holding this
    /// pointer through a block would be reading freed memory.
    /// </summary>
    public void Dispose()
    {
        if (_list == null) return;

        NativeMemory.Free(_list->Events);
        NativeMemory.Free(_list);

        _list = null;
    }
}
