using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Getting a plugin's window ready, and putting it away, without the drawing thread waiting.
/// </summary>
/// <remarks>
/// A plugin builds its whole interface when it is asked, which was measured at 0.6 to 4.7 s, and
/// every page of the application stood still for it. What is pinned here is who waits: a plugin
/// that may be asked from anywhere is asked on another thread, and the panel is only ready once
/// the answer is in it, so the window can be put up at the plugin's size with its face already
/// there. A plugin that needs the drawing thread is asked there, and a window closed before the
/// answer came leaves nothing open in the plugin.
/// </remarks>
public class PluginWindowOpeningTests
{
    /// <summary>An interface that says whether it was put away, and where.</summary>
    private sealed class Face : IPluginEditor
    {
        /// <summary>Whether it was disposed.</summary>
        public bool Disposed { get; private set; }

        /// <summary>The thread it was disposed on.</summary>
        public int DisposedOn { get; private set; }

        /// <summary>Held shut until the test lets the close finish.</summary>
        public ManualResetEventSlim Leaving { get; } = new(true);

        /// <inheritdoc/>
        public (int Width, int Height) Size => (400, 300);

        /// <inheritdoc/>
        public bool CanResize => false;

        /// <inheritdoc/>
        public bool AttachesOffTheDrawingThread => true;

        /// <inheritdoc/>
        public bool Attach(nint window) => true;

        /// <inheritdoc/>
        public void Detach() { }

        /// <inheritdoc/>
        public void Resized(int width, int height) { }

        /// <inheritdoc/>
        public event Action<int, int>? ResizeRequested { add { } remove { } }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposedOn = Environment.CurrentManagedThreadId;

            Leaving.Wait(TimeSpan.FromSeconds(10));

            Disposed = true;
        }
    }

    /// <summary>A plugin whose window takes as long as the test says.</summary>
    private sealed class Slow : IPluginParameters, IPluginWindowSource
    {
        /// <summary>Held shut until the test lets the window be answered.</summary>
        public ManualResetEventSlim Gate { get; } = new(false);

        /// <summary>Whether it has a window of its own to answer with.</summary>
        public bool HasFace { get; init; } = true;

        /// <summary>Every face it has handed out, in order.</summary>
        public List<Face> Faces { get; } = new();

        /// <summary>Whether asking for the window throws instead of answering.</summary>
        public bool Throws { get; init; }

        /// <summary>Whether it may be asked from any thread.</summary>
        public bool Anywhere { get; init; } = true;

        /// <summary>The thread it was last asked on.</summary>
        public int AskedOn { get; private set; }

        /// <inheritdoc/>
        public bool OpensOffTheDrawingThread => Anywhere;

        /// <inheritdoc/>
        public IPluginEditor? OpenEditor()
        {
            AskedOn = Environment.CurrentManagedThreadId;

            Gate.Wait(TimeSpan.FromSeconds(10));

            if (Throws) throw new InvalidOperationException("the plugin fell over");

            if (!HasFace) return null;

            var face = new Face();
            lock (Faces) Faces.Add(face);

            return face;
        }

        /// <inheritdoc/>
        public PluginInfo Info => new("slow", "Slow", "", "", "");

        /// <inheritdoc/>
        public IReadOnlyList<PluginParameter> Parameters() => Array.Empty<PluginParameter>();

        /// <inheritdoc/>
        public double ValueOf(uint id) => 0;

        /// <inheritdoc/>
        public string TextFor(uint id, double value) => "";

        /// <inheritdoc/>
        public void SetValue(uint id, double value) { }

        /// <inheritdoc/>
        public event Action<uint, double>? Edited { add { } remove { } }

        /// <inheritdoc/>
        public event Action? Reloaded { add { } remove { } }

        /// <inheritdoc/>
        public byte[] SaveState() => Array.Empty<byte>();

        /// <inheritdoc/>
        public void LoadState(byte[]? state) { }
    }

    /// <summary>What was posted to the drawing thread, run when the test says.</summary>
    private readonly List<Action> _posted = new();

    /// <summary>Waits for something to be posted, then runs everything posted so far, as the drawing thread would.</summary>
    private void Pump()
    {
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < TimeSpan.FromSeconds(5))
        {
            lock (_posted) if (_posted.Count > 0) break;
            Thread.Sleep(5);
        }

        Action[] waiting;
        lock (_posted)
        {
            waiting = _posted.ToArray();
            _posted.Clear();
        }

        foreach (var job in waiting) job();
    }

    /// <summary>A panel over the plugin, posting into this test.</summary>
    private PluginControlsViewModel Panel(Slow plugin) => new(plugin, post: job => { lock (_posted) _posted.Add(job); });

    /// <summary>Getting the panel ready does not wait for the plugin's window.</summary>
    [Fact]
    public void Getting_ready_does_not_wait_for_the_window()
    {
        var plugin = new Slow();
        var panel = Panel(plugin);

        var clock = Stopwatch.StartNew();
        var ready = panel.PrepareAway();
        clock.Stop();

        Assert.True(clock.ElapsedMilliseconds < 1000, "getting ready waited " + clock.ElapsedMilliseconds + " ms");
        Assert.False(ready.IsCompleted);

        plugin.Gate.Set();
        Pump();

        Assert.True(ready.IsCompleted);
    }

    /// <summary>The panel is ready only once the face is in it, which is when a window may be put up.</summary>
    [Fact]
    public void Ready_means_the_face_is_in_the_panel()
    {
        var plugin = new Slow();
        var panel = Panel(plugin);

        var ready = panel.PrepareAway();
        plugin.Gate.Set();
        Pump();

        Assert.True(ready.IsCompleted);
        Assert.Same(plugin.Faces[0], panel.Editor);
        Assert.True(panel.ShowsFace);
        Assert.NotEqual(Environment.CurrentManagedThreadId, plugin.AskedOn);
    }

    /// <summary>A plugin that needs the drawing thread is asked there, and is ready at once.</summary>
    [Fact]
    public void A_plugin_that_needs_the_drawing_thread_is_asked_there()
    {
        var plugin = new Slow { Anywhere = false };
        plugin.Gate.Set();

        var panel = Panel(plugin);
        var ready = panel.PrepareAway();

        Assert.True(ready.IsCompleted);
        Assert.Same(plugin.Faces[0], panel.Editor);
        Assert.Equal(Environment.CurrentManagedThreadId, plugin.AskedOn);
    }

    /// <summary>Asking to get ready twice while the first is out is the same wait, not a second ask.</summary>
    [Fact]
    public void Getting_ready_twice_is_one_ask()
    {
        var plugin = new Slow();
        var panel = Panel(plugin);

        var first = panel.PrepareAway();
        var second = panel.PrepareAway();

        plugin.Gate.Set();
        Pump();

        Assert.Same(first, second);
        Assert.Single(plugin.Faces);
    }

    /// <summary>A panel put away before the answer came leaves nothing open in the plugin.</summary>
    [Fact]
    public void A_face_that_arrives_after_closing_is_put_away()
    {
        var plugin = new Slow();
        var panel = Panel(plugin);

        var ready = panel.PrepareAway();
        panel.Close();

        plugin.Gate.Set();
        Pump();

        Assert.True(ready.IsCompleted);
        Assert.Null(panel.Editor);
        Assert.True(plugin.Faces[0].Disposed);
    }

    /// <summary>A plugin with no window of its own is ready with the host's knobs.</summary>
    [Fact]
    public void No_window_means_the_knobs()
    {
        var plugin = new Slow { HasFace = false };
        var panel = Panel(plugin);

        var ready = panel.PrepareAway();
        plugin.Gate.Set();
        Pump();

        Assert.True(ready.IsCompleted);
        Assert.Null(panel.Editor);
        Assert.True(panel.ShowsKnobs);
    }

    /// <summary>A plugin that throws on being asked is ready with the knobs, and nothing escapes.</summary>
    [Fact]
    public void A_plugin_that_throws_is_ready_with_the_knobs()
    {
        var plugin = new Slow { Throws = true };
        var panel = Panel(plugin);

        var ready = panel.PrepareAway();
        plugin.Gate.Set();
        Pump();

        Assert.True(ready.IsCompleted);
        Assert.True(panel.ShowsKnobs);
    }

    /// <summary>Closing does not wait for the plugin to let go of its window.</summary>
    /// <remarks>
    /// Letting go still has to happen before the window is destroyed, which is what the task handed
    /// back is for: the window waits for it and nothing else does.
    /// </remarks>
    [Fact]
    public async Task Closing_does_not_wait_for_the_plugin_to_let_go()
    {
        var plugin = new Slow();
        plugin.Gate.Set();

        var panel = Panel(plugin);
        _ = panel.PrepareAway();
        Pump();

        var face = plugin.Faces[0];
        face.Leaving.Reset();

        var clock = Stopwatch.StartNew();
        Task gone = panel.CloseAway();
        clock.Stop();

        Assert.True(clock.ElapsedMilliseconds < 1000, "closing waited " + clock.ElapsedMilliseconds + " ms");
        Assert.Null(panel.Editor);
        Assert.False(gone.IsCompleted);

        face.Leaving.Set();
        Assert.Same(gone, await Task.WhenAny(gone, Task.Delay(TimeSpan.FromSeconds(5))));

        Assert.True(face.Disposed);
        Assert.NotEqual(Environment.CurrentManagedThreadId, face.DisposedOn);
    }

    /// <summary>A panel with nothing open closes at once and owes nothing.</summary>
    [Fact]
    public void Closing_with_no_face_is_done_at_once()
    {
        var panel = Panel(new Slow { HasFace = false });

        Assert.True(panel.CloseAway().IsCompleted);
    }
}
