using System;
using System.Collections.Generic;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// The ports: what the machine has, which of them are open, and what arrives on them.
/// </summary>
/// <remarks>
/// The one place in the application that touches hardware, which is why it is an interface: the
/// five routers, the two surfaces that write back and everything that reads a device list can
/// all be put a question to with nothing plugged in. Ports are named rather than numbered
/// throughout, because an index shifts the moment something else is plugged in or out and a name
/// does not.
///
/// Several devices are open at once and always were. A keyboard, a pad box and a control surface
/// are three ports, and the roles in SETTINGS are how each one's traffic is told from the
/// others'.
///
/// **The thread contract, which is written down in full in <c>docs/threads.md</c>.**
///
/// Everything arriving arrives on the port's own thread, and that thread runs all the way up
/// through the dispatcher and the routers. The drawing thread opens and closes ports and reads
/// what is open. The port table, the running status kept per device and the system exclusive
/// buffers kept per device are all one lock.
///
/// **Above the routers the rule is the other way round: nothing touches a view model where it
/// stands, it posts.** With two deliberate exceptions, both of which cost something to get
/// wrong. A lane being recorded reads the instant here and posts only the write, because posted
/// whole a fast hand piles several values onto whichever line the drawing thread woke on. And a
/// control target reads where a parameter is going rather than where it is, because writes are
/// coalesced onto the drawing thread: twenty notches arriving in the time that thread takes to
/// wake once each added a notch to the same stale number, only the last survived the
/// coalescing, and the parameter moved one notch.
/// </remarks>
public interface IMidiService : IDisposable
{
    /// <summary>Every input the machine is offering now, by name, sorted and without duplicates.</summary>
    IReadOnlyList<string> GetInputDevices();

    /// <summary>
    /// Every output the machine is offering now, on the same terms.
    /// </summary>
    /// <remarks>
    /// **There was no way to ask this until something wanted to choose an output.** Everything
    /// written out of here until now knew its destination without being told: a screen is written
    /// to the port a profile names, and a control surface learns its output from whatever arrived
    /// on the way in, which is the right answer there since a surface speaks and listens on one
    /// port. Sending clock is the first job with a destination somebody has to pick, and a picker
    /// needs a list.
    ///
    /// Kept apart from <see cref="GetInputDevices"/> rather than folded in with a direction on
    /// each row, because the two are not one list: the four jobs a port can be given are all
    /// things it does to us, and a port that plays no part in either direction has no business in
    /// the other's list. The same hardware appears in both under nearly the same name and is two
    /// ports, which is a fact about MIDI rather than something to tidy away.
    /// </remarks>
    IReadOnlyList<string> GetOutputDevices();

    /// <summary>The devices currently open, by name.</summary>
    IReadOnlyList<string> OpenDevices { get; }

    /// <summary>Opens a device and leaves the others alone. True when it is open afterwards.</summary>
    bool Open(string deviceIdOrName);

    /// <summary>
    /// Opens a device's output ahead of writing to it. True when it is open afterwards.
    /// </summary>
    /// <remarks>
    /// **<see cref="Send"/> opens on demand, which is right everywhere but one place.** Opening a
    /// MIDI output was measured at 80 ms on a real port and 197 on a software one, and the one
    /// caller that cannot afford it is the clock: at 120 to the minute a tick is 20.8 ms, so an
    /// open on the first tick of a pass is four to nine ticks missed, on the thread that also
    /// triggers notes, at the moment somebody pressed play. The steady send is 0.24 ms and fits
    /// there comfortably; the open does not.
    ///
    /// So anything that will write on a clock or an audio thread opens the port when it is chosen
    /// rather than when it is first used. Everything else can go on letting <see cref="Send"/> do
    /// it, since a screen being written to a moment late is nothing.
    /// </remarks>
    bool OpenFor(string deviceIdOrName);

    /// <summary>Closes one, and does nothing for a name that is not open.</summary>
    void Close(string deviceIdOrName);

    /// <summary>Closes all of them, which is also what disposing does.</summary>
    void CloseAll();

    /// <summary>
    /// One message off one of the open ports.
    /// </summary>
    /// <remarks>
    /// Raised on whatever thread the port delivers on, which is never the drawing one. Everything
    /// that listens is responsible for getting to its own thread: see <see cref="ControlLink"/>,
    /// which posts, and <see cref="ControlTargets"/>, which coalesces.
    ///
    /// One delivery off a port is not one message, and this is raised once per message rather
    /// than once per delivery. Reading only the first was what left keys sounding: a hand coming
    /// off a chord sends its three note offs in one delivery, while pressing the chord arrives as
    /// three deliveries a millisecond apart, so every press was read and two releases in three
    /// were dropped.
    /// </remarks>
    event EventHandler<MidiMessage>? MessageReceived;

    /// <summary>
    /// Sends bytes to a controller, for the few that listen as well as speak.
    /// </summary>
    /// <remarks>
    /// The application has been input only until now, which is what a controller mostly is. A
    /// few of them have a screen, and a screen is written to rather than read: see
    /// <see cref="ArturiaDisplay"/>.
    ///
    /// The output is opened when it is first needed and kept, because a device with a screen is
    /// written to on every turn of a knob and opening a port per message would be absurd.
    /// </remarks>
    /// <returns>True when it went. False when there is no such output, which is not an error.</returns>
    bool Send(string deviceIdOrName, byte[] bytes);
}
