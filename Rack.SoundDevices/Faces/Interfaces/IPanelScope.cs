using System;

namespace JingleBox2.Rack.SoundDevices.Faces.Interfaces;

/// <summary>
/// The shape a sound device is making, for the panel to draw a picture of.
/// </summary>
/// <remarks>
/// A row of knobs does not tell anybody what a wave with the duty at a fifth and the drive at
/// four actually looks like, and it does not tell them what a filter set that way is doing to a
/// track either. So the panel draws it, and the drawing is worth having only if it is the real
/// thing: what is on screen comes out of the same code that makes the sound, so moving a knob
/// moves the picture because it moved the wave.
///
/// Which is exactly why the panel cannot work it out. What a wave is, how a duty cycle bends it
/// and what drive does to it are the box's business and live where its engine lives. The panel
/// knows how big the picture is and nothing else, so it says how many points it wants and how
/// far it has got, and is handed the curve.
///
/// Both worlds can answer it, which is why it is here rather than beside the played half. A
/// synth traces the wave it is generating; a filter, a compressor or a delay traces the curve it
/// is applying, and there is nothing in the two calls below that assumes a note is what started
/// it.
///
/// Points rather than a formula, and a buffer rather than a fresh array: this is asked sixty
/// times a second while it is moving.
/// </remarks>
public interface IPanelScope
{
    /// <summary>
    /// Fills that buffer with the wave across the window, each value between -1 and 1.
    /// </summary>
    /// <param name="into">As many points as the picture is wide.</param>
    /// <param name="cycles">How much of the wave to fit into the window.</param>
    /// <param name="seconds">How far into a played note, or 0 while nothing is sounding.</param>
    /// <param name="running">Whether a note is sounding, which is what makes it move.</param>
    void Trace(double[] into, double cycles, double seconds, bool running);

    /// <summary>
    /// How long the picture keeps moving after a note, which is about as long as one lasts.
    /// </summary>
    double MotionSeconds => 0.6;

    /// <summary>Told when the sound changed, so the picture is drawn again.</summary>
    event EventHandler? Changed;
}
