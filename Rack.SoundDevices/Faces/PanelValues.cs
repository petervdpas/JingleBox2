using System;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

namespace JingleBox2.Rack.SoundDevices.Faces;

/// <summary>
/// What a sound device's settings are held in, with the announcing done for it.
/// </summary>
/// <remarks>
/// Every sound device has to tell whatever is watching when one of its settings moves: the panel
/// redraws from it, the song is marked as worth saving by it, and anything else showing the
/// same setting follows it. Left to each sound device to remember, one of them will not, and the
/// way that fails is the worst kind: the sound is right and the picture is wrong, so it looks like
/// a drawing fault rather than a missing line, and it is invisible to the hand because a knob you
/// are dragging draws itself from your hand rather than from the setting. It shows up the first
/// time something else moves the value, which is to say the first time a controller does.
///
/// So <see cref="Set"/> is sealed and does the announcing, and a sound device writes its values in
/// <see cref="Write"/> and says whether anything moved. A sound device cannot now move a value
/// quietly, because there is nowhere left to do it.
///
/// <c>Moved</c> is the other half of the same rule: it writes only when the value really
/// is different, so a knob reporting the position it already has does not mark a song as
/// needing to be saved.
/// </remarks>
public abstract class PanelValues : IPanelValues
{
    /// <summary>Told whenever a setting really moves.</summary>
    public Action? Changed { get; set; }

    /// <summary>
    /// Raised alongside <see cref="Changed"/>, for anything showing these rather than owning
    /// them. Named for what everything else in here does when something has happened.
    /// </summary>
    /// <remarks>
    /// A separate thing from <see cref="Changed"/> because there is exactly one owner and any
    /// number of onlookers, and the owner's is set as a property in an object initialiser, which
    /// an event cannot be. Two names for two relationships rather than one name doing both
    /// badly.
    /// </remarks>
    public event Action<string>? Said;

    /// <inheritdoc/>
    public abstract double Get(string key);

    /// <summary>
    /// Sets a value, and says so.
    /// </summary>
    /// <remarks>
    /// Not overridable. This is the whole point of the class.
    ///
    /// A NaN is dropped at the door rather than written. A knob cannot produce one; a file can,
    /// and a NaN reaching a voice spreads through the filter and silences the instrument for
    /// good, with nothing on the panel to say why.
    /// </remarks>
    public void Set(string key, double value)
    {
        if (double.IsNaN(value)) return;

        if (!Write(key, value)) return;

        Changed?.Invoke();
        Said?.Invoke(key);
    }

    /// <summary>Writes one setting, and says whether it actually moved.</summary>
    protected abstract bool Write(string key, double value);

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing, unless a sound device says otherwise. Most of them are numbers from end to end and
    /// have no text to answer with.
    /// </remarks>
    public virtual string GetText(string key) => "";

    /// <summary>
    /// Sets a text setting, and says so.
    /// </summary>
    public void SetText(string key, string value)
    {
        if (!WriteText(key, value ?? "")) return;

        Changed?.Invoke();
        Said?.Invoke(key);
    }

    /// <summary>Writes one text setting, and says whether it actually changed.</summary>
    protected virtual bool WriteText(string key, string value) => false;

    /// <summary>
    /// Says it moved, for the few things that change without going through a key.
    /// </summary>
    protected void Say(string key = "")
    {
        Changed?.Invoke();
        Said?.Invoke(key);
    }

    /// <summary>Writes a number if it really is different, and says whether it was.</summary>
    protected static bool Moved(double was, double now, Action write)
    {
        if (Math.Abs(was - now) < 1e-9) return false;

        write();

        return true;
    }

    /// <summary>
    /// The same for a switch, which a panel hands over as a number either side of a half.
    /// </summary>
    protected static bool Moved(bool was, double now, Action<bool> write)
    {
        bool wanted = now > 0.5;

        if (was == wanted) return false;

        write(wanted);

        return true;
    }

    /// <summary>The same for a setting with whole positions: a mode, a count, a choice.</summary>
    protected static bool Moved(int was, double now, int min, int max, Action<int> write)
    {
        int wanted = (int)Math.Clamp(Math.Round(now), min, max);

        if (was == wanted) return false;

        write(wanted);

        return true;
    }
}
