using JingleBox2.Midi;
using System;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// A knob turned while the song plays, written down as points.
/// </summary>
/// <remarks>
/// The cheapest thing in the whole plan and the one that will feel like the most, because
/// everything it needs was built for something else. The link already resolves to a parameter,
/// takeover has already stopped the value lurching when your hand arrives, the sensing has
/// already worked out whether that control reports a position or a movement, and the router
/// already announces every value it writes. All that is left is to put the number somewhere.
///
/// It is told about a move rather than watching for one, so it holds no timer and no thread.
/// What it needs from the tracker it asks as functions rather than holding as state, because all
/// of it changes underneath: the transport starts and stops, the playing line moves thirty times
/// a second, the song is closed and another opened.
/// </remarks>
public interface IAutomationRecorder
{
    /// <summary>
    /// Whether a hand on a knob is being written down.
    /// </summary>
    /// <remarks>
    /// Off by default and switched on deliberately, because the alternative is a controller
    /// nudged while a song plays quietly editing it. Renoise arms recording the same way and
    /// for the same reason.
    /// </remarks>
    bool Armed { get; set; }

    /// <summary>Told before a lane is first written in a pass, so undo has somewhere to go.</summary>
    /// <remarks>
    /// Once per lane per pass rather than once per point. A hand sweeping a filter across a
    /// pattern is one thing a person did and a hundred and twenty points, and a hundred and
    /// twenty steps would be a hundred and twenty presses of Ctrl+Z to get back to where they
    /// started. The same rule the instrument knobs already use, arrived at from the same
    /// direction.
    ///
    /// The step is taken before a lane that does not yet exist is made, so undo takes the lane
    /// away as well as its points. A lane put back empty would be a parameter that still stopped
    /// moving where the recording stopped.
    /// </remarks>
    Action<Pattern, string>? Taking { get; set; }

    /// <summary>Told after a point is written, since the song now has something unsaved in it.</summary>
    /// <remarks>
    /// Separate from the pattern's own <c>Changed</c> on purpose. Recording happens on whatever
    /// pattern is playing, which is not always the one being looked at, and the pattern being
    /// looked at is the only one anything is subscribed to. Without this a sweep recorded into
    /// the next pattern of the song would leave it looking saved.
    /// </remarks>
    Action? Dirtied { get; set; }

    /// <summary>Ends the pass, so the next one takes its own steps. Called when the clock stops.</summary>
    void Stopped();

    /// <summary>
    /// A parameter moved. Writes a point when it should, and says whether it will.
    /// </summary>
    /// <remarks>
    /// The value arrives in the parameter's own units, since that is what was written to it, and
    /// goes into the lane normalised. That conversion is the whole reason a lane can be pointed
    /// at anything: the lane holds nought to one and the target holds hertz.
    ///
    /// Everything that has to be true of this instant is settled here and the writing is handed
    /// over, so the answer is a promise rather than a report. There is nothing between the two
    /// that can decide otherwise.
    ///
    /// Called on whatever thread the message arrived on, which is not the one patterns are drawn
    /// from. The instant has to be read here, as the message lands: which line the song is on
    /// cannot be asked later, or a sweep would be written wherever the drawing thread happened to
    /// wake and a fast hand would pile several values onto one line.
    /// </remarks>
    /// <returns>True when a point will be written, which is a promise and not a report.</returns>
    bool Moved(ControlMapping? mapping, IControlTarget? target, double value);

    /// <summary>True while a pass has written something. For a light that says so.</summary>
    bool Writing { get; }
}
