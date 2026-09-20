using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// A panel as something the router can tell: what it hears, it sounds on its own instrument.
/// </summary>
/// <remarks>
/// The one place a drawn keyboard and the drawn wheels beside it agree about where they are
/// going, and it exists because they did not. A key clicked on a panel was sounded by that panel,
/// on that panel's instrument; a wheel moved beside it went off to be resolved against whichever
/// half of the application was in front, which is a different answer. So the notes landed on the
/// loose audition bus and the bend was applied to the cursor's track, and a pitch wheel bent
/// nothing whatever while a modulation wheel appeared to work, since that one writes a parameter
/// on the machine and does not care which bus its voices are on.
///
/// **The track is read and never used**, and that is the point rather than an oversight. A panel
/// is about one instrument and knows which: whatever the router says about where an event was
/// going, what a hand does on this panel happens here.
/// </remarks>
/// <param name="panel">Whose panel it is.</param>
public sealed class PanelPlays(ISoundDevicePanel panel) : IPlays
{
    /// <inheritdoc/>
    public void Press(int track, Note note, int volume) => panel.Play(note, volume);

    /// <inheritdoc/>
    public void Let(int track, Note note) => panel.Let(note);

    /// <inheritdoc/>
    public void Bend(int track, double lean) => panel.Bend(lean);

    /// <inheritdoc/>
    public void Modulate(int track, double amount) => panel.Modulate(amount);
}
