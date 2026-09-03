using System.Collections.Generic;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// What a link points at, said in words, and read back out of them.
/// </summary>
/// <remarks>
/// A machine, an effect on a track's chain, or a mixer strip, and the transport beside them.
/// The word for the four of them is target, which is what <see cref="IControlTarget"/> has
/// meant since the beginning. Deliberately not device: this is MIDI, where a device is the box
/// on the desk, and the two ends of the wire may not share a name.
///
/// Not to be confused with <see cref="IControlTargets"/>, which reaches the live thing so a
/// value can be written into it. This one only ever deals in words: which sort of thing a link
/// is on, what it is called, and which parameter, in a form that can be written to a file and
/// read on somebody else's machine. Nothing here touches a machine, a plugin or a mixer.
///
/// One place rather than two, because the page cuts a list of links into cards by exactly the
/// same rule a template is written out by, and two spellings of that rule would eventually
/// disagree. The way that fails is a template that means one thing to whoever exported it and
/// another to whoever opened it.
/// </remarks>
public interface ILinkTargets
{
    /// <summary>
    /// What makes two links the same target, as a string to group by.
    /// </summary>
    /// <remarks>
    /// The ids rather than the names, because the ids are what decides: two machines could be
    /// called the same thing and would still be two machines. A machine's buttons are grouped
    /// with its knobs, since an action is a thing on that machine's face. The mixer is by strip
    /// and not by what a strip has on it, so a track's level, its pan and its mute are one
    /// card. The transport is one thing.
    /// </remarks>
    /// <param name="one">The link to place.</param>
    string KeyOf(ControlMapping one);

    /// <summary>
    /// Whether that kind is one thing to point a controller at rather than one of many.
    /// </summary>
    /// <remarks>
    /// A machine is one of many and names itself: a knob pointed at OddSkilla has nothing to say
    /// to the machine on the next box. The mixer and the pads are each one thing, because what
    /// somebody keeps, hands on or lays down again is the whole layout of the desk rather than
    /// one fader or one pad, and cut the other way they would be a card apiece saying the same
    /// words with a number changed.
    ///
    /// Asked rather than spelled out wherever it is needed, and it is needed in two places that
    /// must agree: how the cards and the template files are cut, and whether a menu with nothing
    /// named has anything to be about. Two spellings of it would drift, and the way that fails is
    /// a page listing what a corner menu says is not there.
    /// </remarks>
    /// <param name="kind">One of the words a link's kind is written down as.</param>
    bool Whole(string kind);

    /// <summary>The one word for what sort of thing it is on: machine, effect, mixer or transport.</summary>
    /// <param name="one">The link to describe.</param>
    string KindOf(ControlMapping one);

    /// <summary>
    /// Which particular one, in a form that means the same on anybody's installation.
    /// </summary>
    /// <remarks>
    /// A machine's own id, a plugin's id as the plugin gave it, or a strip. Nothing for the
    /// transport, which is one thing and needs no name beyond its kind.
    ///
    /// A strip is written the way the mixer says it, so the master is the word and a track is
    /// its number counting from one. The number stored counts from nought; the file is read by
    /// people and says what the screen says.
    /// </remarks>
    /// <param name="one">The link to name.</param>
    string IdOf(ControlMapping one);

    /// <summary>
    /// Which parameter, as the target itself names it.
    /// </summary>
    /// <remarks>
    /// One field for all four kinds, because to a knob they are one question with four
    /// vocabularies: a machine's own key, a plugin's parameter number, one of the mixer's six
    /// words, or one of the transport's five. A file with a field per kind would have three of
    /// them empty on every line.
    /// </remarks>
    /// <param name="one">The link to name.</param>
    string ParameterOf(ControlMapping one);

    /// <summary>
    /// What to head a card with: the thing's own name where anything knows it.
    /// </summary>
    /// <remarks>
    /// <see cref="ControlMapping.Owner"/> where the link kept one. A link made before that was
    /// kept has the name read back out of it, since every one of them was written as the owner
    /// and the parameter run together and taking the parameter off leaves the owner. That works
    /// for machines and not for plugins, whose parameter names are the plugin's and were never
    /// written down here, so an old effect link keeps its id: plain, and still the right card.
    /// </remarks>
    /// <param name="links">Every link on that target, since one may name it where another does not.</param>
    string TitleOf(IEnumerable<ControlMapping> links);

    /// <summary>
    /// Which order the cards come in: machines, effects, the mixer, then the transport.
    /// </summary>
    /// <remarks>
    /// Roughly the order a sound is made in, and it puts the two named after something you own
    /// at the top where they can be found by their names. The mixer and the transport are one
    /// card each at most and are the same card in every song, so they read as the fixtures they
    /// are at the bottom.
    /// </remarks>
    /// <param name="one">The link to place.</param>
    int RankOf(ControlMapping one);

    /// <summary>
    /// A link pointed at what those words describe, or nothing when they describe nothing here.
    /// </summary>
    /// <remarks>
    /// The other direction, and the half that runs on somebody else's machine. Nothing rather
    /// than a guess: a kind this build does not know, or a parameter that is not one of the
    /// words its kind allows, is a line from a newer version or a typo, and either way the
    /// honest answer is to leave it out and say how many were left out.
    ///
    /// The hardware is not filled in here. Which control on which controller is the other half
    /// of a template and is settled where the ports are known.
    /// </remarks>
    /// <param name="kind">One of the four words <see cref="KindOf"/> gives.</param>
    /// <param name="id">Which one, as <see cref="IdOf"/> wrote it.</param>
    /// <param name="parameter">Which parameter, as <see cref="ParameterOf"/> wrote it.</param>
    /// <param name="owner">What the thing is called, for the lists that show it.</param>
    /// <param name="name">What the whole link is called, or nothing to have one built.</param>
    ControlMapping? Point(string kind, string id, string parameter, string owner = "", string name = "");

    /// <summary>How a control reconciles with its value, as a word, and read back from one.</summary>
    /// <param name="pickup">The behaviour to name.</param>
    string Said(ControlPickup pickup);

    /// <summary>The behaviour that word names, or nothing when it names none.</summary>
    /// <param name="said">The word out of a file.</param>
    ControlPickup? Pickup(string said);

    /// <summary>Which way an encoder counts, as a word.</summary>
    /// <param name="turn">The convention to name.</param>
    string Said(ControlTurn turn);

    /// <summary>The convention that word names, or nothing when it names none.</summary>
    /// <param name="said">The word out of a file.</param>
    ControlTurn? Turn(string said);
}
