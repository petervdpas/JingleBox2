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

    /// <summary>The devices currently open, by name.</summary>
    IReadOnlyList<string> OpenDevices { get; }

    /// <summary>Opens a device and leaves the others alone. True when it is open afterwards.</summary>
    bool Open(string deviceIdOrName);

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
