using System;

namespace JingleBox2.Machines;

/// <summary>
/// The shape a machine is making, for the panel to draw a picture of.
/// </summary>
/// <remarks>
/// A machine that generates its sound rather than playing one back has no recording to show, and
/// a row of knobs does not tell anybody what a wave with the duty at a fifth and the drive at
/// four actually looks like. So the panel draws it, and the drawing is worth having only if it is
/// the real thing: what is on screen comes out of the same code that makes the sound, so moving a
/// knob moves the picture because it moved the wave.
///
/// Which is exactly why the panel cannot work it out. What a wave is, how a duty cycle bends it
/// and what drive does to it are the machine's business and live where the machine's engine
/// lives. The panel knows how big the picture is and nothing else, so it says how many points it
/// wants and where in the note it has got to, and is handed the curve.
///
/// Points rather than a formula, and a buffer rather than a fresh array: this is asked sixty
/// times a second while a note runs.
/// </remarks>
public interface IMachineScope
{
    /// <summary>
    /// Fills that buffer with the wave across the window, each value between -1 and 1.
    /// </summary>
    /// <param name="into">As many points as the picture is wide.</param>
    /// <param name="cycles">How much of the wave to fit into the window.</param>
    /// <param name="seconds">How far into a played note, or 0 while nothing is sounding.</param>
    /// <param name="running">Whether a note is sounding, which is what makes it move.</param>
    void Trace(double[] into, double cycles, double seconds, bool running);

    /// <summary>How long the picture keeps moving after a note, which is about as long as one lasts.</summary>
    double MotionSeconds => 0.6;

    /// <summary>Told when the sound changed, so the picture is drawn again.</summary>
    event EventHandler? Changed;
}
