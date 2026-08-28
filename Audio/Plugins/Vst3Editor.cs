using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// A VST3 plugin's own window, put inside one of ours.
/// </summary>
/// <remarks>
/// The plugin is handed an empty window and draws into it. Everything it needs from the host
/// while it is up comes from two places: a frame, which is how it asks to be made a different
/// size, and a run loop, which is how it hears about a click. On X11 both are the host's job
/// because X11 has no run loop of its own, and a plugin that cannot find them will either
/// refuse to open or open and never respond.
/// </remarks>
public sealed unsafe class Vst3Editor : IPluginEditor
{
    /// <summary>The plugin's view, reference counted and released on dispose.</summary>
    private readonly IPlugView* _view;

    /// <summary>
    /// The host as the plugin sees it while its window is up: unmanaged, so the plugin can hold
    /// it, and carrying this editor's number so a static callback can find its way back.
    /// </summary>
    private readonly nint _frame;

    /// <summary>
    /// This editor's number in <see cref="Open_"/>. A number rather than a pointer, because a
    /// managed object cannot be handed to a plugin and a handle would have to be pinned.
    /// </summary>
    private readonly int _slot;

    /// <summary>True between a window being given and taken back. Guards a double attach.</summary>
    private bool _attached;

    /// <summary>
    /// True once everything has been let go. Checked by every method, since the window can be
    /// closed while somebody is still holding the editor.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Private because an editor is only ever made by <see cref="Open"/>, which is the only
    /// place that knows the view has agreed to draw on this platform.
    /// </summary>
    private Vst3Editor(IPlugView* view, nint frame, int slot)
    {
        _view = view;
        _frame = frame;
        _slot = slot;
    }

    /// <inheritdoc/>
    public event Action<int, int>? ResizeRequested;

    /// <summary>
    /// Opens the view a controller offers, or null when it has none or will not draw on this
    /// platform.
    /// </summary>
    /// <remarks>
    /// A plugin that draws on Windows and not on X11 says so when asked about the platform type,
    /// rather than by crashing once it has been given a window, so the question is asked before
    /// anything is built.
    ///
    /// The frame goes in before the window does, because a plugin is allowed to ask for a size
    /// from inside the very call that gives it a window, and one with nowhere to ask has to
    /// either guess or fail.
    /// </remarks>
    internal static Vst3Editor? Open(IEditController* controller)
    {
        if (controller == null || controller->Vtbl == null || controller->Vtbl->CreateView == null) return null;

        using var kind = new NativeText(Vst3Abi.EditorView);
        var view = (IPlugView*)controller->Vtbl->CreateView(controller, kind.Pointer);

        if (view == null || view->Vtbl == null) return null;

        using var platform = new NativeText(Vst3Abi.PlatformWindowType);

        if (view->Vtbl->IsPlatformTypeSupported(view, platform.Pointer) != Vst3Abi.ResultOk)
        {
            Release(view);
            return null;
        }

        int slot;
        lock (Registry) slot = _nextSlot++;

        nint frame = CreateFrame(slot);
        var editor = new Vst3Editor(view, frame, slot);

        lock (Registry) Open_[slot] = editor;

        view->Vtbl->SetFrame(view, (void*)frame);

        return editor;
    }

    /// <inheritdoc/>
    /// <remarks>Nought by nought for a disposed editor, or one whose view refuses to say.</remarks>
    public (int Width, int Height) Size
    {
        get
        {
            if (_disposed) return (0, 0);

            var rect = new ViewRect();
            if (_view->Vtbl->GetSize(_view, &rect) != Vst3Abi.ResultOk) return (0, 0);

            return (Math.Max(0, rect.Width), Math.Max(0, rect.Height));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Compared against the ABI's awkward true, which is nought: false here is one, so testing
    /// the result against nought the ordinary way would answer backwards.
    /// </remarks>
    public bool CanResize => !_disposed && _view->Vtbl->CanResize(_view) == Vst3Abi.ResultTrue;

    /// <inheritdoc/>
    public bool Attach(nint window)
    {
        if (_disposed || _attached || window == 0) return false;

        using var platform = new NativeText(Vst3Abi.PlatformWindowType);

        if (_view->Vtbl->Attached(_view, (void*)window, platform.Pointer) != Vst3Abi.ResultOk) return false;

        _attached = true;
        return true;
    }

    /// <inheritdoc/>
    public void Detach()
    {
        if (_disposed || !_attached) return;

        _attached = false;
        _view->Vtbl->Removed(_view);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The rectangle is given as a size at the origin, since a plugin's view is measured in its
    /// own window rather than in the screen.
    /// </remarks>
    public void Resized(int width, int height)
    {
        if (_disposed || width <= 0 || height <= 0) return;

        var rect = new ViewRect { Left = 0, Top = 0, Right = width, Bottom = height };
        _view->Vtbl->OnSize(_view, &rect);
    }

    /// <summary>
    /// Takes the window back, unpoints the frame, and lets the view go, in that order.
    /// </summary>
    /// <remarks>
    /// The frame is taken away before the view is let go, or the plugin is left holding a
    /// pointer to something that has gone: the frame is freed at the end of this method and a
    /// plugin still holding it would be writing into the heap.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Detach();

        _view->Vtbl->SetFrame(_view, null);

        lock (Registry) Open_.Remove(_slot);

        Release(_view);

        if (_frame != 0) NativeMemory.Free((void*)_frame);
    }

    /// <summary>
    /// Gives back one reference on a VST3 object, guarding a null table, which a plugin that
    /// failed part way through construction can leave behind.
    /// </summary>
    private static void Release(void* instance)
    {
        if (instance == null) return;

        var unknown = (FUnknown*)instance;
        if (unknown->Vtbl != null && unknown->Vtbl->Release != null) unknown->Vtbl->Release(instance);
    }

    /// <summary>Frames by the number written into them, so a callback can find its editor.</summary>
    private static readonly Dictionary<int, Vst3Editor> Open_ = new();

    /// <summary>Held over every read and write of the two statics above.</summary>
    private static readonly object Registry = new();

    /// <summary>
    /// The next number to give out. Never reused, so a callback arriving late from a window that
    /// has already gone finds nothing rather than finding somebody else's editor.
    /// </summary>
    private static int _nextSlot = 1;

    /// <summary>
    /// The one frame table, shared by every open window. A table is code rather than state, so
    /// one is enough and it is never freed.
    /// </summary>
    private static nint _frameTable;

    /// <summary>A frame carries its own number, so one table serves every open window.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Frame
    {
        /// <summary>The table, which has to be the first word or the plugin calls rubbish.</summary>
        public nint Vtbl;

        /// <summary>Which editor this frame belongs to. See <see cref="_slot"/>.</summary>
        public int Slot;
    }

    /// <summary>
    /// Makes one frame, building the shared table on the first call. The table is the root's
    /// three then IPlugFrame's one, in the order the header declares them.
    /// </summary>
    private static nint CreateFrame(int slot)
    {
        lock (Registry)
        {
            if (_frameTable == 0)
            {
                var table = (nint*)NativeMemory.AllocZeroed(4, (nuint)sizeof(nint));

                table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&FrameQuery;
                table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&FrameKeepAlive;
                table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&FrameKeepAlive;
                table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, void*, ViewRect*, int>)&FrameResize;

                _frameTable = (nint)table;
            }
        }

        var frame = (Frame*)NativeMemory.AllocZeroed(1, (nuint)sizeof(Frame));
        frame->Vtbl = _frameTable;
        frame->Slot = slot;

        return (nint)frame;
    }

    /// <summary>
    /// A frame is an IPlugFrame and the root interface, and on X11 it is also where the run loop
    /// is handed over. A plugin looks for the run loop here as well as on the host context, and
    /// one that cannot find it either way will not draw.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int FrameQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.PlugFrameId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
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
    /// AddRef and Release both. A frame is freed by its editor rather than by a count, so it
    /// always answers one.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint FrameKeepAlive(void* self) => 1;

    /// <summary>The plugin asking the host to make its window a different size.</summary>
    /// <remarks>
    /// Answered before the window has actually changed. The host is expected to resize and then
    /// call back with onSize, which is what the control does. A frame whose editor has already
    /// gone answers a refusal rather than doing nothing, since the plugin is entitled to know
    /// its request went nowhere.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int FrameResize(void* self, void* view, ViewRect* size)
    {
        var frame = (Frame*)self;
        if (frame == null || size == null) return Vst3Abi.NoInterface;

        Vst3Editor? editor;
        lock (Registry) Open_.TryGetValue(frame->Slot, out editor);

        if (editor == null) return Vst3Abi.NoInterface;

        int width = size->Width;
        int height = size->Height;

        editor.ResizeRequested?.Invoke(width, height);

        return Vst3Abi.ResultOk;
    }
}
