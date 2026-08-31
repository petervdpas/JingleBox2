using System;
using System.Collections.Generic;
using JingleBox2.Midi.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Templates: writing one controller's layout for one thing out, and reading one back in.
/// </summary>
/// <remarks>
/// The half of linking that leaves this machine. Everything else about a link is about the room
/// you are sitting in; a template is the part that is true of the hardware and the target rather
/// than of you, and is therefore the part worth sending to somebody else.
///
/// Nothing here touches the live links. Describing takes mappings and gives a document; taking
/// gives mappings back and leaves it to the caller to lay them down, which is what keeps the
/// whole of this answerable without a controller, a song or a window.
/// </remarks>
public interface IControlTemplates
{
    /// <summary>Where templates are kept, made if it is not there yet.</summary>
    /// <remarks>
    /// Beside the machines and the controller profiles, under the application folder, because it
    /// is the same sort of thing: something you own, that arrived from outside, that the program
    /// reads rather than writes. Only a default: a template can be written anywhere and opened
    /// from anywhere, since the point of one is that it travels.
    /// </remarks>
    string Folder();

    /// <summary>What to call the file, from what is in it.</summary>
    /// <remarks>
    /// The controller and the target, in the words a person would use, cut down to what every
    /// file system will take. It is a suggestion in a save box and nothing reads it back: two
    /// templates may share a name and both still work.
    /// </remarks>
    /// <param name="template">The template to name.</param>
    string FileName(ControlTemplate template);

    /// <summary>
    /// One controller's links on one target, written as a template.
    /// </summary>
    /// <remarks>
    /// The caller has already cut the list, since the page and the file agree about what a
    /// target is: see <see cref="ILinkTargets"/>. Handed links on two targets this would write
    /// the first one's and quietly drop the rest, so it refuses instead.
    /// </remarks>
    /// <param name="controller">The controller as its profile calls it, never a port name.</param>
    /// <param name="links">Its links on one target.</param>
    /// <param name="named">
    /// What a control is called on the front of the device, asked by channel and number. Left
    /// out, the file carries no legends and is read by its numbers, which is what it decides by
    /// anyway. A delegate rather than the profiles themselves, so a template can be written
    /// with no controller, no profile folder and no disc.
    /// </param>
    ControlTemplate? Describe(string controller, IEnumerable<ControlMapping> links, Func<int, int, string>? named = null);

    /// <summary>
    /// The links a template describes, ready to be laid down.
    /// </summary>
    /// <remarks>
    /// The port is settled here, since this is the one part of a template that is about the
    /// machine it arrives on: the controller is named as its profile calls it and the ports are
    /// whatever this computer happens to spell them. A controller that is not plugged in keeps
    /// the name the file carried, so the links are there when it comes back rather than being
    /// refused for a cable.
    ///
    /// What cannot be read is left out and counted rather than failing the lot: a template from
    /// a newer version is mostly this version's, and the useful answer is the part that works
    /// plus a line saying how much did not.
    /// </remarks>
    /// <param name="template">What was opened.</param>
    /// <param name="ports">The MIDI ports this computer has, for working out which is the controller.</param>
    /// <param name="called">What a port's profile calls it, or the port itself where none does.</param>
    ControlTemplateReading Take(ControlTemplate? template, IEnumerable<string>? ports = null, Func<string, string>? called = null);

    /// <summary>Writes it out whole, so a half-written file cannot replace a good one.</summary>
    /// <param name="path">Where to write it.</param>
    /// <param name="template">What to write.</param>
    void Write(string path, ControlTemplate template);

    /// <summary>Reads one back, or nothing when the file is not one of these.</summary>
    /// <remarks>
    /// Nothing rather than an exception for a file that is not a template, since picking the
    /// wrong file is an ordinary mistake and not a fault. A file that is one and is damaged
    /// comes back as nothing too, and the caller says so.
    /// </remarks>
    /// <param name="path">The file to read.</param>
    ControlTemplate? Open(string path);
}
