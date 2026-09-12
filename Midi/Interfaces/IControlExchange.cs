using System.Collections.Generic;
using JingleBox2.Midi.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// One place a controller can be pointed at, and the hook a template is exchanged through.
/// </summary>
/// <remarks>
/// There are three of these and they are the whole of the list: a sound device's face, wherever
/// it is standing, the mixer, and the pads. Each is a thing somebody points a controller at and
/// each has the same three questions about it, so they are asked here once rather than answered
/// three times in three pages that would drift.
///
/// **It goes both ways, and only one direction was ever built.** A template is exchanged in from
/// the Menu, which is <see cref="Take"/>, and that half worked everywhere. The other half is
/// saying what is wired here now, which is what Ctrl+Shift+M marks, and that existed only for a
/// drawn face: the mixer and the pads are ordinary controls, so a template applied to either lit
/// nothing at all and read exactly like a template that had not been applied.
///
/// Nothing is automatic. A template sits in the block until somebody chooses it, and choosing it
/// lays its links down on the desk, where they stay for the session and are written down. That is
/// the whole of what applying one is, and it is why this is an exchange rather than a mode: what
/// arrives is links like any other, displacing what held their controls and what was pointed at
/// their target, and nothing anywhere remembers that a template was involved.
///
/// What it does not decide is whether a link answers. That is the other rule and it belongs to
/// the resolver: a link on a sound device answers only while that device is the one in front of
/// you, so a template exchanged in here is silent until you are looking at the thing it is about.
/// The mixer and the pads name a strip and a pad outright, so theirs answer from anywhere.
/// </remarks>
public interface IControlExchange
{
    /// <summary>
    /// Which sort of thing this is, in the word <see cref="ILinkTargets.KindOf"/> uses.
    /// </summary>
    /// <remarks>
    /// Asked of that rule rather than spelled out, so the hook, the cards on the MIDI CC page and
    /// a file on the disc cut the links one way. Three spellings of what counts as one target is
    /// how a template comes to mean one thing to whoever exported it and another to whoever
    /// applies it.
    /// </remarks>
    string Kind { get; }

    /// <summary>
    /// Which particular one, or nothing for every one of that kind.
    /// </summary>
    /// <remarks>
    /// A sound device names itself, since a knob pointed at OddSkilla has nothing to say to the
    /// device beside it. The mixer and the pads name none: a link there is on a strip or on a pad
    /// and the whole desk is one thing to point a controller at, so what somebody keeps, hands on
    /// or lays down again is the whole layout rather than one fader's worth of it.
    /// </remarks>
    string Id { get; }

    /// <summary>
    /// The templates there are for this, in the order the MIDI CC page lists them.
    /// </summary>
    /// <remarks>
    /// Read out of the block rather than cut here, which is the whole of what the block is for:
    /// the links are cut once, at startup and whenever they move, and everything that shows a
    /// template reads the same list. One controller against this thing is one line.
    /// </remarks>
    IReadOnlyList<ControlTemplate> Offered();

    /// <summary>
    /// Exchanges one in, which is what choosing it on the Menu does.
    /// </summary>
    /// <remarks>
    /// Laid down through <see cref="ControlLink.Take"/>, the one door a batch of links goes
    /// through, so a template chosen from a face and one imported off the disc cannot come to
    /// mean different things. One control does one job, so each arriving link takes back whatever
    /// has been pointed at the same thing since, and choosing the same template twice leaves what
    /// it did the first time.
    ///
    /// The reading says how many links, how much of the file this build had no word for, and
    /// whether the controller is plugged in. A controller in the other room still has its links
    /// laid down and they wait for it, since leaving one on the other desk is not a decision to
    /// unwire it.
    /// </remarks>
    /// <param name="template">The template, as the block holds it.</param>
    ControlTemplateReading Take(ControlTemplate? template);

    /// <summary>
    /// Whether something is already pointed at that control.
    /// </summary>
    /// <remarks>
    /// The marking half. Asked with the very thing a control offers, since that object is what a
    /// page already carries and is the only thing on a screen that knows what it is pointed at,
    /// so a fader, a pad and a knob on a face are one question rather than three.
    ///
    /// By the target and never by the controller. The mark says this fader has something on it,
    /// not which box: two controllers pointed at one fader is two links and one mark, which is
    /// right, since what it warns about is that pointing here replaces something.
    /// </remarks>
    /// <param name="control">What that control offers a knob.</param>
    bool Wired(ControlMapping? control);
}
