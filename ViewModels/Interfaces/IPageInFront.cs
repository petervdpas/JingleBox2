namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// Which of the pages a controller can be pointed at is the one in front of you.
/// </summary>
/// <remarks>
/// **The page is the gate, exactly as a face is.** A link on a sound device is silent unless that
/// device's face is the one you are looking at, which is what makes one knob able to be
/// OddSkilla's tune and something else at the same time: at most one of them can ever answer. The
/// mixer and the pads had no such test. A mixer link names strip one outright and a pad link names
/// a pad, so both answered from anywhere, and a knob carrying a mixer template and a machine
/// template fired both at once, every time, wherever you were.
///
/// What that looked like was a knob moving a machine on the rack and quietly moving the pan of
/// track one with it, which then marked the song unsaved: two writes and one turn, with only one
/// of them wanted and nothing on the screen saying the other had happened.
///
/// So the same rule everywhere: a link answers while the thing it is pointed at is the thing in
/// front of you. **A page or a window**, since the mixer is taken out into one of its own and is
/// still the mixer.
///
/// Asked of what the application knows about where you are rather than of the views themselves.
/// Whether a control is really on the screen is <c>IsEffectivelyVisible</c>, and a page that is
/// not the chosen tab is hidden by something above it rather than by itself, so a test on its own
/// <c>IsVisible</c> reads every page as showing at once; Avalonia raises no change for the
/// effective one, so anything counting that would be right once and stale for the rest of the run.
///
/// The transport is the one thing left ungated, since its four keys mean the same on every page,
/// exactly as the space bar does.
/// </remarks>
public interface IPageInFront
{
    /// <summary>True while the mixer is the page showing.</summary>
    bool Mixer { get; }

    /// <summary>
    /// True while the pads are showing, which is either of the two pages that draw them.
    /// </summary>
    /// <remarks>
    /// FIRE is where a show is run from and PADS is where one is set up, and a pad box is worth
    /// having on both: a pad hit while filling in the pads is how you hear what you have just
    /// pointed at a file.
    /// </remarks>
    bool Pads { get; }
}
