using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The envelope a plugin's two halves post to each other, which the host has to supply.
/// </summary>
/// <remarks>
/// A VST3 plugin comes in two pieces that are not allowed to know about each other: the part
/// that makes the sound and the part that draws the knobs. When one has something to tell the
/// other, it does not make a message of its own. It asks the host for one, writes on it, and
/// hands it over. So a host with nowhere to get a message from is a host where the two halves
/// of every plugin have nothing to say.
///
/// Refusing looks harmless and is not. The plugin gets a null pointer back and most of them do
/// not check, because no real host has ever refused. Serum's interface asks for a message the
/// moment it opens, reads a field off what it is given, and dies on the spot. That is the whole
/// story of a plugin that crashed on being shown and worked perfectly otherwise.
///
/// Reference counting here is real, unlike the host's permanent objects: the plugin owns what
/// it is given and lets go of it when it is done, and the memory has to go then.
/// </remarks>
internal static unsafe class Vst3Messages
{
    /// <summary>Held while either table is built, so two plugins asking at once cannot both build it.</summary>
    private static readonly object Gate = new();

    /// <summary>
    /// The two tables, shared by every message in the process. A table is code rather than
    /// state, so one of each is enough and neither is ever freed.
    /// </summary>
    private static nint* _messageTable;

    /// <inheritdoc cref="_messageTable"/>
    private static nint* _attributesTable;

    /// <summary>What is written on one message, kept on this side of the wall.</summary>
    private sealed class Note
    {
        /// <summary>The message's name, as a C string the plugin may hold on to.</summary>
        public nint Name;

        /// <summary>
        /// What has been written on the message, by attribute name. Boxed rather than typed
        /// because the ABI allows four kinds and a getter of the wrong kind has to be refused:
        /// that is what the pattern match in each getter is for.
        /// </summary>
        public readonly Dictionary<string, object> Values = new(StringComparer.Ordinal);

        /// <summary>Lumps of bytes handed out by pointer, kept until they are replaced.</summary>
        public readonly Dictionary<string, nint> Lumps = new(StringComparer.Ordinal);

        /// <summary>The attribute list object, made once and living as long as the message.</summary>
        public nint Attributes;
    }

    /// <summary>
    /// One host object as the plugin sees it: a table of functions, a count of who is holding
    /// it, and a way back to what it actually is.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Block
    {
        /// <summary>The table, which has to be the first word or the plugin calls rubbish.</summary>
        public nint Vtbl;

        /// <summary>
        /// How many holders there are. Raised and lowered with interlocked operations, since the
        /// two halves of a plugin need not be on the same thread.
        /// </summary>
        public int Held;

        /// <summary>
        /// Nothing, and it has to stay: it is what puts <see cref="Handle"/> on an eight byte
        /// boundary, which is where the compiler would have put it in C.
        /// </summary>
        public int Padding;

        /// <summary>
        /// A pinned handle on the managed <see cref="Note"/> behind this block. The only way
        /// from a plain C callback back to an object the collector owns.
        /// </summary>
        public nint Handle;
    }

    /// <summary>
    /// Makes a message when a plugin asks the host for one.
    /// </summary>
    /// <remarks>
    /// Answers to both the message and the attribute list, because a plugin that wants a bare
    /// attribute list asks for one the same way.
    /// </remarks>
    public static int Create(byte* cid, byte* iid, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        *result = null;

        if (Vst3Abi.SameId(cid, Vst3Abi.MessageId) || Vst3Abi.SameId(iid, Vst3Abi.MessageId))
        {
            *result = Message();
            return Vst3Abi.ResultOk;
        }

        if (Vst3Abi.SameId(cid, Vst3Abi.AttributeListId) || Vst3Abi.SameId(iid, Vst3Abi.AttributeListId))
        {
            var note = new Note();
            var list = Attributes(note);

            note.Attributes = (nint)list;

            *result = list;
            return Vst3Abi.ResultOk;
        }

        return Vst3Abi.NotImplemented;
    }

    /// <summary>
    /// Builds one message and the attribute list that belongs to it. The list is made here
    /// rather than when it is first asked for, because a plugin is entitled to ask for it and
    /// write on it before anybody looks.
    /// </summary>
    private static void* Message()
    {
        var note = new Note();

        var block = (Block*)NativeMemory.AllocZeroed(1, (nuint)sizeof(Block));

        block->Vtbl = (nint)MessageTable();
        block->Held = 1;
        block->Handle = GCHandle.ToIntPtr(GCHandle.Alloc(note));

        note.Attributes = (nint)Attributes(note);

        return block;
    }

    /// <summary>
    /// Builds an attribute list over a note. Its own block and its own count, but it is freed by
    /// the message rather than by that count: see <see cref="LetGo"/>.
    /// </summary>
    private static void* Attributes(Note note)
    {
        var block = (Block*)NativeMemory.AllocZeroed(1, (nuint)sizeof(Block));

        block->Vtbl = (nint)AttributesTable();
        block->Held = 1;
        block->Handle = GCHandle.ToIntPtr(GCHandle.Alloc(note));

        return block;
    }

    /// <summary>
    /// What a block is really about. Null for a block that has already been freed, which is what
    /// a plugin calling on a message it has let go of looks like.
    /// </summary>
    private static Note? Behind(void* self)
    {
        var block = (Block*)self;
        if (block == null || block->Handle == 0) return null;

        try
        {
            return GCHandle.FromIntPtr(block->Handle).Target as Note;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the message table once: the root's three, then IMessage's three, in the order the
    /// header declares them. The order is the whole contract, since a plugin calls by position
    /// and nothing checks.
    /// </summary>
    private static nint* MessageTable()
    {
        lock (Gate)
        {
            if (_messageTable != null) return _messageTable;

            var table = (nint*)NativeMemory.AllocZeroed(6, (nuint)sizeof(nint));

            table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&MessageQuery;
            table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&Hold;
            table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&LetGo;
            table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*>)&NameOf;
            table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void>)&Rename;
            table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, void*>)&AttributesOf;

            _messageTable = table;
            return table;
        }
    }

    /// <summary>
    /// The same for an attribute list: the root's three, then the eight setters and getters
    /// IAttributeList declares, in pairs by kind.
    /// </summary>
    private static nint* AttributesTable()
    {
        lock (Gate)
        {
            if (_attributesTable != null) return _attributesTable;

            var table = (nint*)NativeMemory.AllocZeroed(11, (nuint)sizeof(nint));

            table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&AttributesQuery;
            table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&Hold;
            table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&LetGo;
            table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, long, int>)&SetWhole;
            table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, long*, int>)&GetWhole;
            table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, double, int>)&SetFraction;
            table[6] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, double*, int>)&GetFraction;
            table[7] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, char*, int>)&SetWords;
            table[8] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, char*, uint, int>)&GetWords;
            table[9] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void*, uint, int>)&SetLump;
            table[10] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, uint*, int>)&GetLump;

            _attributesTable = table;
            return table;
        }
    }

    /// <summary>
    /// A message is an IMessage and the root interface, and nothing else. The count goes up on
    /// the way out, because whatever comes back from a query is already held: a plugin that
    /// releases it once has released what it asked for and not the message itself.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MessageQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.MessageId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            Keep(self);
            *result = self;
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>
    /// An attribute list is an IAttributeList and the root interface, and nothing else. The
    /// count goes up on the way out, as it does for a message.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AttributesQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.AttributeListId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            Keep(self);
            *result = self;
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>The plugin taking one more hold on a message or a list.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint Hold(void* self) => Keep(self);

    /// <summary>One more holder. Separate from the one the plugin calls, which may not be.</summary>
    private static uint Keep(void* self)
    {
        var block = (Block*)self;
        if (block == null) return 1;

        return (uint)System.Threading.Interlocked.Increment(ref block->Held);
    }

    /// <summary>
    /// One holder fewer. The last one out frees the object, and a message frees what was
    /// written on it.
    /// </summary>
    /// <remarks>
    /// The attribute list belongs to the message and goes when it does. Freeing it here rather
    /// than on its own count is deliberate: a plugin is given the list without being made a
    /// holder of it, which is how the specification has it, so its own count would never reach
    /// nought and the list would leak once per message.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint LetGo(void* self)
    {
        var block = (Block*)self;
        if (block == null) return 0;

        int left = System.Threading.Interlocked.Decrement(ref block->Held);
        if (left > 0) return (uint)left;

        var note = Behind(self);

        if (note != null && note.Attributes != 0 && note.Attributes != (nint)self)
        {
            var attributes = (Block*)note.Attributes;
            note.Attributes = 0;

            Forget(attributes);
            NativeMemory.Free(attributes);
        }

        if (note != null && note.Attributes == 0) Empty(note);

        Forget(block);
        NativeMemory.Free(block);

        return 0;
    }

    /// <summary>
    /// Frees the handle that ties a block to its note, so the collector can have the note back.
    /// Guarded, because a block whose handle has already gone is what a double release looks
    /// like and freeing a handle twice throws.
    /// </summary>
    private static void Forget(Block* block)
    {
        if (block == null || block->Handle == 0) return;

        try { GCHandle.FromIntPtr(block->Handle).Free(); } catch (Exception) { }

        block->Handle = 0;
    }

    /// <summary>Frees everything the host allocated on a plugin's behalf for one message.</summary>
    private static void Empty(Note note)
    {
        lock (note)
        {
            if (note.Name != 0)
            {
                Marshal.FreeCoTaskMem(note.Name);
                note.Name = 0;
            }

            foreach (var lump in note.Lumps.Values)
            {
                if (lump != 0) NativeMemory.Free((void*)lump);
            }

            note.Lumps.Clear();
            note.Values.Clear();
        }
    }

    /// <summary>
    /// What the message is called. The pointer belongs to the host and stays valid until the
    /// message is renamed or freed, which is what the plugin expects.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte* NameOf(void* self)
    {
        var note = Behind(self);
        if (note == null) return null;

        lock (note) return (byte*)note.Name;
    }

    /// <summary>
    /// Naming the message, which is the first thing a sender does. The old name is freed here
    /// rather than left for the message to free, since a plugin that renames a message in a loop
    /// would otherwise leak one string per turn.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Rename(void* self, byte* id)
    {
        var note = Behind(self);
        if (note == null) return;

        string name = id == null ? "" : Marshal.PtrToStringUTF8((nint)id) ?? "";

        lock (note)
        {
            if (note.Name != 0) Marshal.FreeCoTaskMem(note.Name);

            note.Name = Marshal.StringToCoTaskMemUTF8(name);
        }
    }

    /// <summary>
    /// The message's attribute list, which is where everything except the name is written. The
    /// plugin is not made a holder of it: the message owns it.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void* AttributesOf(void* self)
    {
        var note = Behind(self);

        return note == null ? null : (void*)note.Attributes;
    }

    /// <summary>
    /// An attribute's name as a string to look it up by. Null reads as empty rather than being
    /// refused, since a plugin naming nothing is asking about one particular attribute and will
    /// find it again by the same nothing.
    /// </summary>
    private static string Key(byte* id) => id == null ? "" : Marshal.PtrToStringUTF8((nint)id) ?? "";

    /// <summary>Writing a whole number onto the message.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetWhole(void* self, byte* id, long value)
    {
        var note = Behind(self);
        if (note == null) return Vst3Abi.NoInterface;

        lock (note) note.Values[Key(id)] = value;

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Reading one back. Refused for a name that is not there and for one holding another kind,
    /// which is what the specification asks for and what stops a fraction being read as bits.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetWhole(void* self, byte* id, long* value)
    {
        var note = Behind(self);
        if (note == null || value == null) return Vst3Abi.NoInterface;

        lock (note)
        {
            if (!note.Values.TryGetValue(Key(id), out var held) || held is not long whole) return Vst3Abi.NoInterface;

            *value = whole;
        }

        return Vst3Abi.ResultOk;
    }

    /// <summary>Writing a fractional number onto the message.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetFraction(void* self, byte* id, double value)
    {
        var note = Behind(self);
        if (note == null) return Vst3Abi.NoInterface;

        lock (note) note.Values[Key(id)] = value;

        return Vst3Abi.ResultOk;
    }

    /// <inheritdoc cref="GetWhole"/>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetFraction(void* self, byte* id, double* value)
    {
        var note = Behind(self);
        if (note == null || value == null) return Vst3Abi.NoInterface;

        lock (note)
        {
            if (!note.Values.TryGetValue(Key(id), out var held) || held is not double fraction) return Vst3Abi.NoInterface;

            *value = fraction;
        }

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Words on a message are sixteen-bit characters, and stored here as what they are.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetWords(void* self, byte* id, char* value)
    {
        var note = Behind(self);
        if (note == null) return Vst3Abi.NoInterface;

        string words = value == null ? "" : new string(value);

        lock (note) note.Values[Key(id)] = words;

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Reading words back into the plugin's own buffer. The room is given in bytes and the
    /// characters are two bytes each, so the count has to be halved and one taken off for the
    /// terminator; a buffer with room for fewer than one character is refused rather than
    /// written into.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetWords(void* self, byte* id, char* value, uint bytes)
    {
        var note = Behind(self);
        if (note == null || value == null || bytes < 2) return Vst3Abi.NoInterface;

        string words;

        lock (note)
        {
            if (!note.Values.TryGetValue(Key(id), out var held) || held is not string found) return Vst3Abi.NoInterface;

            words = found;
        }

        int room = (int)(bytes / 2) - 1;
        int count = Math.Min(words.Length, room);

        for (int index = 0; index < count; index++) value[index] = words[index];

        value[count] = '\0';

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Writing a lump of bytes onto the message. Copied rather than kept by pointer, because the
    /// plugin's buffer is its own and may be gone by the time the other half reads it. The copy
    /// under the same name is freed first, so writing twice does not leak.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetLump(void* self, byte* id, void* data, uint bytes)
    {
        var note = Behind(self);
        if (note == null) return Vst3Abi.NoInterface;

        string key = Key(id);

        lock (note)
        {
            if (note.Lumps.TryGetValue(key, out nint old) && old != 0) NativeMemory.Free((void*)old);

            void* copy = bytes == 0 ? null : NativeMemory.Alloc(bytes);

            if (copy != null && data != null) Buffer.MemoryCopy(data, copy, bytes, bytes);

            note.Lumps[key] = (nint)copy;
            note.Values[key] = bytes;
        }

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Reading a lump back, as a pointer into the host's own copy. It stays valid until the same
    /// name is written again or the message is freed, which is why the copies are kept on the
    /// note rather than freed as they are read.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetLump(void* self, byte* id, void** data, uint* bytes)
    {
        var note = Behind(self);
        if (note == null || data == null || bytes == null) return Vst3Abi.NoInterface;

        string key = Key(id);

        lock (note)
        {
            if (!note.Lumps.TryGetValue(key, out nint lump)) return Vst3Abi.NoInterface;
            if (!note.Values.TryGetValue(key, out var held) || held is not uint size) return Vst3Abi.NoInterface;

            *data = (void*)lump;
            *bytes = size;
        }

        return Vst3Abi.ResultOk;
    }
}
