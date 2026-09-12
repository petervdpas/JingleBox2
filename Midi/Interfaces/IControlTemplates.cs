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
    /// Every template a list of links comes to: one per controller per thing pointed at.
    /// </summary>
    /// <remarks>
    /// **The cut is <see cref="ILinkTargets.KeyOf"/> and then the controller**, which is what a
    /// template is: what your nanoKONTROL2 does to OddSkilla. Two desks pointed at one machine
    /// are two templates and never one, since a link answers only its own controller's messages,
    /// so the two can never compete and are two things somebody keeps, hands on or lays down
    /// apart.
    ///
    /// **The controller is what a profile calls the device and never the port it arrived on.** A
    /// MiniLab is <c>Minilab3 MIDI</c> and <c>Minilab3 ALV</c> to this machine and is one desk to
    /// the hand on it, so cutting by the port makes two templates of one, both under the same
    /// name, each covering every one of the other's links: two identical cards on the page and
    /// two identical lines on a machine's face. It is the name a person reads and the name a file
    /// carries, so it is the name the cut is by.
    ///
    /// One rule, said here, because the page cuts its cards by it and the file is written by it
    /// and now the block is filled by it: three spellings would eventually disagree, and the way
    /// that fails is a template that means one thing to whoever exported it and another to
    /// whoever reads the block.
    ///
    /// A group that describes nothing is left out rather than carried as an empty template, which
    /// is what <see cref="Describe"/> answers for a target it cannot write.
    /// </remarks>
    /// <param name="links">Everything pointed at anything, in any order.</param>
    /// <param name="called">
    /// What a controller is called, asked by the port name a link carries. Left out, the port's
    /// own name is used, which is the one thing in a template that does not travel.
    /// </param>
    /// <param name="named">
    /// What a control is called on the front of the device, asked by the port, the channel and
    /// the number. Left out, the templates carry no legends and read by their numbers.
    /// </param>
    /// <returns>The templates, in the order the page lists them.</returns>
    IReadOnlyList<ControlTemplate> Cut(
        IEnumerable<ControlMapping>? links,
        Func<string, string>? called = null,
        Func<string, int, int, string>? named = null);

    /// <summary>
    /// Whether a link is one the template is about.
    /// </summary>
    /// <remarks>
    /// **The cut said backwards**, and here because it must be the same rule: a page showing a
    /// template's rows and a face offering that template have to agree about which links it
    /// covers, and two spellings of that end as a card listing a knob the template it is headed
    /// by does not carry.
    ///
    /// A target that names nothing takes every link of its kind, which is the mixer: a link there
    /// is on a strip and the whole desk is one thing to point a controller at, so the strip is
    /// written on each line rather than in the target.
    ///
    /// The controller is compared as a profile calls it rather than as a port, since a device on
    /// two ports is one desk.
    /// </remarks>
    /// <param name="template">The template.</param>
    /// <param name="one">The link to place.</param>
    /// <param name="called">
    /// What a controller is called, asked by the port name a link carries. Left out, the port's
    /// own name is compared, which is right where nothing knows any better.
    /// </param>
    bool Covers(ControlTemplate? template, ControlMapping? one, Func<string, string>? called = null);

    /// <summary>
    /// The links a template describes, ready to be laid down.
    /// </summary>
    /// <remarks>
    /// **A link names the controller and never a port**, so nothing written here is a port. It
    /// has to be the name, because a box arrives on more than one of them: a MiniLab is
    /// <c>Minilab3 MIDI</c> and <c>Minilab3 ALV</c>, the knobs come in on whichever one the
    /// program the device is running delivers on, and a link holding the other answers nothing at
    /// all. The template applies, says how many controls it carried, and moves nothing. The name
    /// is the one spelling every port of a box shares, and it is what a link made by hand holds.
    ///
    /// The ports are looked through for one answer only, which is whether this computer can see
    /// the controller at all, and that decides the wording rather than the links: a controller in
    /// the other room lays its links down exactly as one on the desk does, and they wait for it.
    ///
    /// What cannot be read is left out and counted rather than failing the lot: a template from
    /// a newer version is mostly this version's, and the useful answer is the part that works
    /// plus a line saying how much did not.
    /// </remarks>
    /// <param name="template">What was opened.</param>
    /// <param name="ports">The MIDI ports this computer has, for saying whether the controller is here.</param>
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
