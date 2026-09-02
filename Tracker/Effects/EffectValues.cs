using System;
using JingleBox2.Rack.Faces.Interfaces;
using JingleBox2.Tracker.Effects.Interfaces;

namespace JingleBox2.Tracker.Effects;

/// <summary>
/// A face's values, when what is behind the face is one of our effects on a chain.
/// </summary>
/// <remarks>
/// The panel reads and writes what its controls stand for, and for an effect that is the engine
/// itself: it is in this process, it holds its own knobs, and it is the thing the audio is going
/// through. There is nothing between them to keep in step, which is the whole reason an effect of
/// ours is simpler than a plugin: no round trip, no second copy of the values, no state lump.
///
/// Text settings are none. An effect is handed audio and has nothing to browse.
/// </remarks>
/// <param name="engine">The effect that is running, whose knobs these are.</param>
public sealed class EffectValues(IEffectEngine engine) : IPanelValues
{
    /// <summary>
    /// Raised when a value here moved, so the panel drawing them follows.
    /// </summary>
    /// <remarks>
    /// Needed for the same reason a machine's values raise it: a knob on the desk can be pointed
    /// at an effect and turned from there, and without this the panel would never hear about it.
    /// </remarks>
    public event Action<string>? Said;

    /// <inheritdoc/>
    public double Get(string key) => engine.ValueOf(key);

    /// <inheritdoc/>
    /// <remarks>
    /// A write that would not move the knob is dropped rather than announced, since a controller
    /// resting against an end sends the same number over and over and every one of those would
    /// redraw the panel and reread the block in the chain.
    ///
    /// Compared as single words, because that is what an engine keeps its knobs in: a value
    /// written and read back comes out a hair different, and a comparison at full precision
    /// therefore called every write a change. Narrowed, the test says exactly what it means,
    /// which is whether this would move anything at all.
    /// </remarks>
    public void Set(string key, double value)
    {
        if ((float)engine.ValueOf(key) == (float)value) return;

        engine.SetValue(key, value);

        Said?.Invoke(key);
    }
}
