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
    private readonly IPlugView* _view;
    private readonly nint _frame;
    private readonly int _slot;

    private bool _attached;
    private bool _disposed;

    private Vst3Editor(IPlugView* view, nint frame, int slot)
    {
        _view = view;
        _frame = frame;
        _slot = slot;
    }

    /// <summary>The plugin asking to be a different size.</summary>
    public event Action<int, int>? ResizeRequested;

    /// <summary>
    /// Opens the view a controller offers, or null when it has none or will not draw on this
    /// platform.
    /// </summary>
    internal static Vst3Editor? Open(IEditController* controller)
    {
        if (controller == null || controller->Vtbl == null || controller->Vtbl->CreateView == null) return null;

        using var kind = new NativeText(Vst3Abi.EditorView);
        var view = (IPlugView*)controller->Vtbl->CreateView(controller, kind.Pointer);

        if (view == null || view->Vtbl == null) return null;

        // A plugin that draws on Windows and not on X11 says so here rather than by crashing
        // once it has been given a window.
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

        // The frame goes in before the window does: a plugin is allowed to ask for a size
        // from inside the call that gives it a window.
        view->Vtbl->SetFrame(view, (void*)frame);

        return editor;
    }

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

    public bool CanResize => !_disposed && _view->Vtbl->CanResize(_view) == Vst3Abi.ResultTrue;

    public bool Attach(nint window)
    {
        if (_disposed || _attached || window == 0) return false;

        using var platform = new NativeText(Vst3Abi.PlatformWindowType);

        if (_view->Vtbl->Attached(_view, (void*)window, platform.Pointer) != Vst3Abi.ResultOk) return false;

        _attached = true;
        return true;
    }

    public void Detach()
    {
        if (_disposed || !_attached) return;

        _attached = false;
        _view->Vtbl->Removed(_view);
    }

    public void Resized(int width, int height)
    {
        if (_disposed || width <= 0 || height <= 0) return;

        var rect = new ViewRect { Left = 0, Top = 0, Right = width, Bottom = height };
        _view->Vtbl->OnSize(_view, &rect);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Detach();

        // The frame is taken away before the view is let go, or the plugin is left holding a
        // pointer to something that has gone.
        _view->Vtbl->SetFrame(_view, null);

        lock (Registry) Open_.Remove(_slot);

        Release(_view);

        if (_frame != 0) NativeMemory.Free((void*)_frame);
    }

    private static void Release(void* instance)
    {
        if (instance == null) return;

        var unknown = (FUnknown*)instance;
        if (unknown->Vtbl != null && unknown->Vtbl->Release != null) unknown->Vtbl->Release(instance);
    }

    // ---- The frame, which is the host as the plugin sees it while its window is up

    /// <summary>Frames by the number written into them, so a callback can find its editor.</summary>
    private static readonly Dictionary<int, Vst3Editor> Open_ = new();

    private static readonly object Registry = new();

    private static int _nextSlot = 1;

    private static nint _frameTable;

    /// <summary>A frame carries its own number, so one table serves every open window.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Frame
    {
        public nint Vtbl;
        public int Slot;
    }

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int FrameQuery(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.PlugFrameId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        // A plugin looks for the run loop here as well as on the host context, and one that
        // cannot find it either way will not draw.
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && Vst3Abi.SameId(id, Vst3Abi.RunLoopId))
        {
            *result = PluginRunLoop.Instance();
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint FrameKeepAlive(void* self) => 1;

    /// <summary>The plugin asking the host to make its window a different size.</summary>
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

        // Answered before the window has actually changed. The host is expected to resize and
        // then call back with onSize, which is what the control does.
        editor.ResizeRequested?.Invoke(width, height);

        return Vst3Abi.ResultOk;
    }
}
