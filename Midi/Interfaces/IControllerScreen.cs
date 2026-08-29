using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// A controller's screen, whatever kind of screen it is.
/// </summary>
/// <remarks>
/// Two lines of words and a reading. That is the whole of what every screen anybody has put on a
/// MIDI controller can be told, and it is deliberately the least of what any of them can do: a
/// MiniLab 3 draws a value as a ring and a Mackie display cannot draw anything, so the contract
/// is the part they share and each protocol does what it can with it.
///
/// This exists because the application used to hold one protocol and send it to everything. That
/// was written for a MiniLab 3 and worked, and the moment a second Arturia keyboard arrived it
/// was wrong twice over, and the first way was a surprise. The KeyLab has a screen, it takes the
/// very same messages a MiniLab's does, and it showed nothing: the writing went to whichever ports
/// were ticked as Controls, and a KeyLab's screen is on its DAW port. So the protocol was right,
/// the address was right, and the thing nobody had written down was which port to say it on.
///
/// The second way is the one that would have shown up on somebody else's desk. Every other
/// controller was written to as well, on the grounds that a device which is not listening costs a
/// few bytes. A few bytes down a port nobody reads is fine. A settings write aimed at a device
/// that is not the one it was written for is not, and an MPD218 was receiving them.
///
/// So which screen a device has is a fact about the device, and it is where every other fact
/// about a device lives: its file. <c>IControllerProfiles.ScreenOn</c> is the
/// question, a device whose file says nothing has no screen, and a device with no file has no
/// screen either. That is the same rule the rest of the profile works by, which is that a file
/// may add names and shape and may never add capability.
/// </remarks>
public interface IControllerScreen
{
    /// <summary>Whether this is the screen that device has.</summary>
    /// <remarks>
    /// Asked before anything is sent, so a protocol only ever writes to hardware that speaks it.
    /// </remarks>
    /// <param name="device">The port, named as the operating system names it.</param>
    bool Writes(string? device);

    /// <summary>
    /// What the screen says when nothing else is happening.
    /// </summary>
    /// <remarks>
    /// Set once and everything else lands on top of it: turn a knob and the reading appears, take
    /// your hand off and the screen comes back to this. Remembered, so a controller plugged in
    /// halfway through gets the same greeting as one that was there from the start.
    /// </remarks>
    /// <param name="first">The top line.</param>
    /// <param name="second">The line under it.</param>
    void Standing(string first, string second);

    /// <summary>Two lines of words on one device, and nothing else.</summary>
    /// <param name="device">The port to write to.</param>
    /// <param name="first">The top line.</param>
    /// <param name="second">The line under it.</param>
    void Say(string device, string first, string second);

    /// <summary>
    /// A control being moved: what it is, what it reads, and where it is in its range.
    /// </summary>
    /// <param name="device">The port to write to.</param>
    /// <param name="kind">Which picture the reading is drawn in, for a screen that draws.</param>
    /// <param name="fraction">Nought to one, which a screen that draws uses for the bar.</param>
    /// <param name="what">The parameter's name.</param>
    /// <param name="reads">What it now says.</param>
    /// <param name="hide">
    /// True to have the screen go back to what it was showing after a moment, which is what a
    /// knob wants: the reading matters while your hand is on it and not afterwards.
    /// </param>
    void Moved(string device, ScreenKind kind, double fraction, string what, string reads, bool hide = true);

    /// <summary>Forgets what it knew about a device, for one that has gone.</summary>
    /// <param name="device">The port that has gone.</param>
    void Gone(string device);

    /// <summary>
    /// Says the standing text again, to every screen, as though none had been written to.
    /// </summary>
    /// <remarks>
    /// Because a controller is not ready the instant the operating system lists it. A keyboard
    /// powered on a second before the application starts is on the bus, opens for writing, accepts
    /// the message and shows nothing, and the identical bytes sent by hand a minute later appear
    /// at once. That was measured on a KeyLab mkII rather than reasoned about, and there is no
    /// message that asks a device whether it is ready.
    ///
    /// So the greeting is said twice, and this is the second time. What each device was last shown
    /// is forgotten first, or the repeat would be dropped as a picture it already has.
    /// </remarks>
    void Again();
}
