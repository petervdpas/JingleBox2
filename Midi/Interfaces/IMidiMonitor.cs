using System;
using System.Collections.Generic;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Which keys are down and where the two wheels are being held, whatever is doing it.
/// </summary>
/// <remarks>
/// What a drawn keyboard shows. It is an interface because the thing that draws keys should not
/// have to know where notes come from, and because a keyboard can then be put a question to
/// without a port, a window or a hand: press a key on one of these and read what lit.
///
/// The wheels are here for the same reason the keys are, which is that a drawn one is a picture
/// of the thing under somebody's hand rather than a second control beside it. **They are read
/// and never written from a panel**: a wheel on the screen that could be dragged would be a
/// second wheel disagreeing with the first, and it would jump the moment the hardware moved.
///
/// The two halves of a press and the two wheel positions are all it holds. What a note went on
/// to sound, and for how long, is a different question with a different answer, and it is not
/// this one.
///
/// The wheels come from <see cref="IPanelWheels"/> rather than being said again here, since a
/// panel reads them through that and this is where they are answered from: two contracts saying
/// the same two numbers would be two things to keep in step for no gain.
/// </remarks>
public interface IMidiMonitor : IPanelWheels
{
    /// <summary>The semitones held down now.</summary>
    IReadOnlyCollection<int> Down { get; }

    /// <summary>True while that key is down, whoever put it there.</summary>
    bool Holds(int semitone);

    /// <summary>
    /// A key pressed by something that plays it itself, which is a drawn keyboard.
    /// </summary>
    /// <remarks>
    /// A key on the hardware arrives on its way past to being played and needs no telling. One
    /// pressed on a panel is sounded by that panel, so it says so here instead: putting it back
    /// into the stream would sound it twice.
    /// </remarks>
    void Pressed(int semitone);

    /// <summary>And let go of.</summary>
    void Released(int semitone);

    /// <summary>Told when a key goes down or comes up, on the thread it arrived on.</summary>
    event EventHandler? Changed;

}
