using System.Text.Json.Serialization;

namespace JingleBox2.Midi;

/// <summary>
/// One control of a template: which knob, and what it moves.
/// </summary>
/// <remarks>
/// The channel and the number are what decides, since that is what arrives on the wire and it
/// is the same on every one of that model. The name beside them is the legend printed on the
/// front, written so the file can be read, and nothing is resolved from it: a device in another
/// of its programs sends different numbers under the same legends, and a file that resolved by
/// name would quietly point at the wrong knob rather than at none.
/// </remarks>
public sealed class ControlTemplateControl
{
    /// <summary>What is printed on the front of it, where the profile knows. For reading only.</summary>
    public string Control { get; set; } = "";

    /// <summary>1 to 16, as the message says it.</summary>
    public int Channel { get; set; } = 1;

    /// <summary>Which controller or note number, 0 to 127.</summary>
    public int Cc { get; set; }

    /// <summary>
    /// Which kind of message the control sends: the word <c>note</c>, or nothing for a
    /// controller.
    /// </summary>
    /// <remarks>
    /// A word rather than a number out of an enum, like everything else in this file, so it can
    /// be read and corrected by somebody who has never seen this code.
    ///
    /// Left out of the file where it is empty, which is every line of every template written
    /// before the pads joined this layer and almost every line since: a knob, a fader and a
    /// button all send controllers, and it is pad boxes that send notes.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Sends { get; set; } = "";

    /// <summary>
    /// Which parameter, as the target itself names it.
    /// </summary>
    /// <remarks>
    /// A machine's own key, a plugin's parameter number, one of the mixer's six words, or one
    /// of the transport's five. One field for all four, because to a knob they are one question
    /// with four vocabularies and a field per kind would leave three empty on every line.
    /// </remarks>
    public string Parameter { get; set; } = "";

    /// <summary>What the whole thing is called in a list. Built from the target where absent.</summary>
    public string Name { get; set; } = "";

    /// <summary>How the hardware and the software are reconciled when they disagree.</summary>
    /// <remarks>
    /// A fact about the hardware rather than about the person, so it travels. A controller with
    /// a profile has it corrected on arrival anyway, since a file describing the device beats
    /// anything worked out by watching it.
    /// </remarks>
    public string Pickup { get; set; } = "";

    /// <summary>Which way an encoder counts, where it is one.</summary>
    public string Turn { get; set; } = "";

    /// <summary>
    /// Which track it is nailed to, counting from one, or nought for one that follows you.
    /// </summary>
    /// <remarks>
    /// Only ever read for a machine or an effect. A mixer link names its strip in the target
    /// above, and the transport belongs to no track at all.
    ///
    /// Left out of the file where it is nought, which is almost every line: a link that follows
    /// the track you are working on is the ordinary kind, and a column of zeroes down a file
    /// meant to be read by people says nothing on every line of it.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Track { get; set; }

    /// <summary>
    /// Which strip, on a mixer template, written the way the mixer says it.
    /// </summary>
    /// <remarks>
    /// The master is the word master and a track is its number counting from one, which is what
    /// the screen says and what a file read by people should say.
    ///
    /// On the line rather than on the target above, because the mixer is one thing to point a
    /// controller at: what you keep and hand on is the whole layout of the desk, so a template
    /// covers every strip that controller touches and each line says which. A template written
    /// before this named its one strip in the target instead, and is still read that way.
    ///
    /// Nothing on any other kind of line, and left out of the file where it is empty.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Strip { get; set; } = "";
}
