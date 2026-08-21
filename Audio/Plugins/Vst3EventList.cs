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
    [StructLayout(LayoutKind.Sequential)]
    private struct List
    {
        public nint Vtbl;
        public int Count;
        public int Capacity;
        public Vst3Event* Events;
    }

    private static nint _table;
    private static readonly object Gate = new();

    private List* _list;
    private readonly int _capacity;

    public Vst3EventList(int capacity)
    {
        _capacity = Math.Max(1, capacity);

        _list = (List*)NativeMemory.AllocZeroed(1, (nuint)sizeof(List));
        _list->Vtbl = Table();
        _list->Capacity = _capacity;
        _list->Events = (Vst3Event*)NativeMemory.AllocZeroed((nuint)_capacity, (nuint)sizeof(Vst3Event));
    }

    public void* Pointer => _list;

    public int Count => _list == null ? 0 : _list->Count;

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

    private Vst3Event* Next()
    {
        if (_list == null || _list->Count >= _capacity) return null;

        var slot = _list->Events + _list->Count;
        _list->Count++;

        // Everything not written below stays as it was left, so the shared fields are cleared
        // rather than inherited from whatever note used this slot last block.
        slot->BusIndex = 0;
        slot->PpqPosition = 0;
        slot->Flags = Vst3Abi.LiveEvent;

        return slot;
    }

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint KeepAlive(void* self) => 1;

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int EventCount(void* self)
    {
        var list = (List*)self;
        return list == null ? 0 : list->Count;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int EventAt(void* self, int index, Vst3Event* into)
    {
        var list = (List*)self;
        if (list == null || into == null || index < 0 || index >= list->Count) return Vst3Abi.NoInterface;

        // Handed back by value: the plugin is given a copy rather than a pointer into ours.
        *into = list->Events[index];
        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// A plugin sending an event of its own, which only happens on the outgoing list. Taken
    /// and dropped, because nothing here listens to a plugin's notes yet.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AddEvent(void* self, Vst3Event* added) => Vst3Abi.ResultOk;

    public void Dispose()
    {
        if (_list == null) return;

        NativeMemory.Free(_list->Events);
        NativeMemory.Free(_list);

        _list = null;
    }
}
