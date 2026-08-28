using System;
using JingleBox2.Midi;
using JingleBox2.Controllers;

namespace JingleBox2.Controllers.Interfaces;

/// <summary>
/// The scripts that stand between a controller and the rest of the program.
/// </summary>
/// <remarks>
/// A codec is one file per controller, and its whole job is to turn what a device actually
/// sends into something this application already understands. It sits after the wire and before
/// the routing, so everything downstream carries on knowing nothing about any particular
/// hardware, which is the property worth protecting: a device nobody has written a file for
/// still works, because a message nothing translates is passed through untouched.
///
/// That is the point of doing it here rather than inside the routers. A codec cannot add a
/// feature and cannot take one away. It can only say that these bytes mean those bytes, which
/// is a small enough thing to hand to a stranger's file.
///
/// Two folders, as machines have two: beside the program is what ships and is never written to,
/// under the application folder is what this installation has. The first run fills the second
/// from the first. Somebody who deletes a codec has deleted it, and can take it again.
///
/// Matched on the port's name for now. Identity is the better key, since a MiniLab answers a
/// universal identity request with the same eleven bytes on every operating system while its
/// port is called something different on each, and moving the match onto that is the next thing
/// this wants. See docs/hardware-integration.md.
/// </remarks>
public interface IControllerCodecs : IDisposable
{
    /// <summary>
    /// Reads every codec again, from scratch.
    /// </summary>
    /// <remarks>
    /// Called at startup and on every save of a file in the folder, which is the difference
    /// between writing one of these and enjoying writing one of these. A person adding a
    /// controller should edit, touch the knob, and see. Not edit, restart, replug, remember
    /// what they were testing.
    /// </remarks>
    void Reload();

    /// <summary>
    /// What the application should read instead of what arrived. Null when it was swallowed.
    /// </summary>
    /// <remarks>
    /// The message itself when nothing translates it, which is the ordinary case and the one
    /// that must stay free: a device with no codec pays one dictionary lookup and nothing else.
    ///
    /// A codec answers in one of four ways. Nothing at all, or nil, leaves the message as it
    /// arrived, and that is the path taken by every message a codec does not care about. True
    /// keeps it and false eats it, for a script saying so in as many words. A table is a message
    /// built out of what it handed back, with everything it did not mention left alone.
    /// </remarks>
    MidiMessage? Read(MidiMessage message);
}
