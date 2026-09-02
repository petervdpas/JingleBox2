using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JingleBox2.Controllers;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Machines;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Machines.Records;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
/// <remarks>
/// The links half of the application, answering the one question a machine's face is allowed to
/// ask about it: which of the desks in this room is there a template for.
///
/// A template is one controller against one thing it is pointed at, and it is the links
/// themselves rather than a file: it is the card the MIDI CC page draws, cut by
/// <see cref="ILinkTargets"/>, which is the same rule a file is written by. So a machine nobody
/// has pointed anything at lists nothing, and one with a nanoKONTROL2 pointed at it lists that
/// and nothing else.
///
/// A line for each of them, and one more at the foot to start learning, which turns over exactly
/// the mode Ctrl+Shift+M turns over. That last one is why the part is worth having on the face at
/// all: the keystroke works everywhere and is a thing you have to know, and a machine you are
/// looking at should be able to say it out loud.
///
/// Flat. What it offers is a short list and a switch, and neither is a tree.
///
/// Which machine is asked for rather than held, since one of these serves a panel and the panel
/// is shown a different machine as somebody works. Nothing for a page with none open.
/// </remarks>
public sealed class ControlMenu : IMachineMenu
{
    /// <summary>
    /// Which sort of thing this menu is about, in the word <see cref="ILinkTargets.KindOf"/> uses.
    /// </summary>
    /// <remarks>
    /// A machine, or the mixer. Asked of the naming rule rather than spelled out, so this and
    /// the MIDI CC page's cards cut the links by one rule and cannot drift into listing
    /// different things.
    /// </remarks>
    private readonly string _kind;

    /// <summary>
    /// Which particular one, or nothing for every one of that kind.
    /// </summary>
    /// <remarks>
    /// A machine names itself, since a knob pointed at OddSkilla has nothing to do with the
    /// machine on the next box. The mixer names none: a link there is on a strip and the whole
    /// desk is one thing to point a controller at, so what somebody wants to see on the mixer is
    /// what their nanoKONTROL2 does to the mixer rather than a menu per fader.
    /// </remarks>
    private readonly Func<string> _which;

    /// <summary>What that machine is called on the front of it, for the wording.</summary>
    private readonly Func<string> _named;

    /// <summary>
    /// Where the links live, asked each time rather than held.
    /// </summary>
    /// <remarks>
    /// A panel is built by whoever happens to be showing a machine, and there is no path from
    /// there to the object the application made at startup, so the fallback is the door
    /// <see cref="Views.Pointable"/> and the instrument panel already go through to offer a link
    /// at all. Asked per press, so one of these made before the application had finished starting
    /// still answers.
    ///
    /// A question rather than the door itself, so what happens with no desk at all can be put a
    /// question to. A static cannot be stood in front of, and that case is exactly the one worth
    /// checking: it is what a panel shown by something that is not this application would meet.
    /// </remarks>
    private readonly Func<ControlLink?> _desk;

    /// <summary>What is known about the controllers plugged in. Shared, since it remembers.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>What a target is called, so this cuts the links exactly as a card and a file do.</summary>
    private readonly ILinkTargets _naming;

    /// <summary>What one thing can offer about the hardware pointed at it.</summary>
    /// <param name="which">
    /// Which one, by the id songs and templates write down, or nothing for every one of that
    /// kind, which is what the mixer wants.
    /// </param>
    /// <param name="named">What it is called, for the wording. Left out, the id is used.</param>
    /// <param name="kind">
    /// Which sort of thing, in the word the naming rule uses. Left out, a machine, since that is
    /// what almost every one of these is about.
    /// </param>
    /// <param name="desk">Where the links live. Left out, the one the application set up.</param>
    /// <param name="profiles">
    /// What is known about the controllers plugged in. Left out, one of its own; the application
    /// hands the same one to everything, since what a device is doing is remembered in it.
    /// </param>
    /// <param name="naming">What a target is called, shared with the page so the two agree.</param>
    public ControlMenu(
        Func<string> which,
        Func<string>? named = null,
        Func<ControlLink?>? desk = null,
        IControllerProfiles? profiles = null,
        ILinkTargets? naming = null,
        string kind = LinkTargets.Machine)
    {
        _which = which;
        _kind = kind;
        _named = named ?? which;
        _desk = desk ?? Door;
        _profiles = profiles ?? new ControllerProfiles();
        _naming = naming ?? new LinkTargets();
    }

    /// <summary>
    /// Where a line saying what happened goes, or nowhere.
    /// </summary>
    /// <remarks>
    /// A callback rather than a line of its own, because a machine's face has no room to say
    /// anything and what has room differs per host: the designer has a status line under its
    /// title, a window holding nothing but a panel has none. Whatever happens is written to the
    /// log either way, so an outcome is never lost for want of somewhere to put it.
    /// </remarks>
    public Action<string>? Told { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<MachineMenuItem> Read()
    {
        string id = _which() ?? "";

        if (Desk is not { } link) return Array.Empty<MachineMenuItem>();

        if (Names && id.Length == 0) return Array.Empty<MachineMenuItem>();

        string called = _named() is { Length: > 0 } word ? word : id;

        var offers = Templates(link, id)
            .Select(one => Pointed(link, called, one.Key, one.Value))
            .ToList();

        offers.Add(Learning(link, called));

        return offers;
    }

    /// <summary>
    /// The templates on this thing, which is its links cut by controller.
    /// </summary>
    /// <remarks>
    /// Exactly the cards the MIDI CC page draws, by the same rule and for the same reason: one
    /// controller against one thing it is pointed at is what a template is, and two spellings of
    /// that would eventually disagree about what this thing has.
    ///
    /// Cut by <see cref="ILinkTargets"/> and never by comparing an id here: how exact an id is is
    /// that rule's business. A menu that names no particular one takes every link of its kind,
    /// which is the mixer, where a link is on a strip and the whole desk is one thing to point a
    /// controller at.
    ///
    /// Grouped by what the profile calls the device rather than by the port, since that is the
    /// name a person reads and a device on two ports is one desk.
    /// </remarks>
    /// <param name="link">Where the links live.</param>
    /// <param name="id">Which one, or nothing for every one of this kind.</param>
    private IEnumerable<KeyValuePair<string, List<ControlMapping>>> Templates(ControlLink link, string id) =>
        link.Desk
            .Where(one => Mine(one, id))
            .GroupBy(one => _profiles.Called(one.Device), StringComparer.OrdinalIgnoreCase)
            .OrderBy(one => one.Key, StringComparer.OrdinalIgnoreCase)
            .Select(one => new KeyValuePair<string, List<ControlMapping>>(one.Key, one.ToList()));

    /// <summary>Whether that link is one of this menu's.</summary>
    /// <param name="one">The link to place.</param>
    /// <param name="id">Which one this menu is about, or nothing for every one of its kind.</param>
    private bool Mine(ControlMapping one, string id) =>
        string.Equals(_naming.KindOf(one), _kind, StringComparison.Ordinal)
        && (!Names || string.Equals(_naming.IdOf(one), id, StringComparison.Ordinal));

    /// <summary>Whether this menu is about one particular thing rather than a whole kind.</summary>
    /// <remarks>
    /// A machine names itself and the mixer does not, and the difference decides two things: what
    /// counts as one of this menu's links, and whether a menu with nothing named has anything to
    /// be about at all. A machine with no id is a page with nothing open; the mixer with no id is
    /// the mixer.
    /// </remarks>
    private bool Names => !string.Equals(_kind, LinkTargets.Mixer, StringComparison.Ordinal);

    /// <summary>
    /// One control surface pointed at this machine, and laying its template down again.
    /// </summary>
    /// <remarks>
    /// Headed with the surface, since that is what somebody is choosing between: which of the
    /// boxes on this desk do I want driving this machine.
    ///
    /// Laid down through <see cref="ControlLink.Take"/>, which is the one door a batch of links
    /// goes through and keeps the rules a link made by hand keeps. A template already in force
    /// therefore comes back exactly as it was, and one whose knobs have since been pointed
    /// somewhere else on this machine takes them back.
    /// </remarks>
    /// <param name="link">Where the links live.</param>
    /// <param name="called">What the machine is called, for the wording.</param>
    /// <param name="controller">The controller as its profile calls it.</param>
    /// <param name="links">Its links on this machine.</param>
    private MachineMenuItem Pointed(
        ControlLink link,
        string called,
        string controller,
        IReadOnlyList<ControlMapping> links) =>
        new((controller.Length > 0 ? controller : Anonymous) + Beside + Counted(links.Count))
        {
            Option = MachineMenuOptions.Surfaces,
            Tip = "Points that controller at " + called + " the way this template says. One "
                  + "control does one job, so each of them takes back whatever has been pointed "
                  + "at the same thing since.",
            Chosen = () =>
            {
                link.Take(links);

                Say("Pointed " + (controller.Length > 0 ? controller : Anonymous)
                    + " at " + called + ": " + Counted(links.Count) + ".");
            }
        };

    /// <summary>
    /// The line that starts learning, which is Ctrl+Shift+M and nothing else.
    /// </summary>
    /// <remarks>
    /// The same switch and not a second way of doing it: two spellings of one mode would
    /// eventually disagree, and the way that fails is a menu saying the mode is off while the
    /// keystroke has it on. It says which way it is about to turn it, since the menu is read
    /// again every time it is opened and there is no other sign of the mode on a machine's face.
    /// </remarks>
    /// <param name="link">Where the links live, and what holds the mode.</param>
    /// <param name="called">What the machine is called, for the wording.</param>
    private MachineMenuItem Learning(ControlLink link, string called) =>
        new(link.IsLinking ? "Stop learning" : "Learn a control")
        {
            Option = MachineMenuOptions.Learn,
            Tip = link.IsLinking
                ? "Turns the mode off again. The same as pressing Ctrl+Shift+M."
                : "The same as pressing Ctrl+Shift+M. Rest the pointer on one of " + called
                  + "'s controls until it glows, then touch the control on your desk.",
            Chosen = () =>
            {
                link.IsLinking = !link.IsLinking;

                Say(link.IsLinking
                    ? "Point at one of " + called + "'s controls and touch the control on your desk."
                    : "Stopped learning.");
            }
        };

    /// <summary>Where the links live, as this one was told to find them.</summary>
    private ControlLink? Desk => _desk();

    /// <summary>The application's own, which is what a panel it is showing gets.</summary>
    private static ControlLink? Door() => ControlLink.Current;

    /// <summary>Writes it down, and puts it where the host keeps a line if it keeps one.</summary>
    /// <param name="said">What happened.</param>
    private void Say(string said)
    {
        if (said.Length == 0) return;

        Log.Write(LogArea.Midi, () => "links: " + said);

        Told?.Invoke(said);
    }

    /// <summary>A count and the word after it, singular where it has to be.</summary>
    /// <param name="many">How many.</param>
    private static string Counted(int many) =>
        many.ToString(CultureInfo.InvariantCulture) + (many == 1 ? " control" : " controls");

    /// <summary>What sits between a heading and the count beside it.</summary>
    private const string Beside = "  \u00B7  ";

    /// <summary>What a link naming no controller is called, since it still has to be listed.</summary>
    private const string Anonymous = "Learned before controllers were recorded";

}
