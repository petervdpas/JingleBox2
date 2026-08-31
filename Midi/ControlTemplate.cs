using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JingleBox2.Midi;

/// <summary>
/// One controller's layout for one thing, as a file.
/// </summary>
/// <remarks>
/// A template is what your nanoKONTROL2 does to OddSkilla, and it is the same sentence on
/// anybody's installation: a machine's id is what decides its engine and travels in its zip, a
/// plugin's id and its parameter numbers come from the plugin, and a mixer strip is a number.
/// So a template is a thing that can be handed to somebody else, which is what it is for.
///
/// The one part that does not travel is the port. One nanoKONTROL2 is called
/// <c>nanoKONTROL2 _ CTRL</c> by the ALSA sequencer and <c>nanoKONTROL2 _ SLIDER/KNOB</c> by
/// rawmidi, and Windows spells it a third way, so the file names the controller as its profile
/// calls it and the port is worked out on arrival. That is the only conversion an import does.
///
/// Written by hand as easily as by the program: it is a small object of plain words, and every
/// value in it is a word rather than a number out of an enum, so a template can be read,
/// corrected and sent on by somebody who has never seen this code.
/// </remarks>
public sealed class ControlTemplate
{
    /// <summary>What this file is, so a file picked up out of a folder can say what it is.</summary>
    /// <remarks>
    /// Spelled out rather than left to the naming policy, which would write it jingleBox: the
    /// word is the application's own name and a person opening the file should see it the way
    /// it is written everywhere else.
    /// </remarks>
    [JsonPropertyName("jinglebox")]
    public string JingleBox { get; set; } = Kind;

    /// <summary>What a control template says it is.</summary>
    public const string Kind = "control-template";

    /// <summary>
    /// Which version of this shape, so a later one can be told from a damaged one.
    /// </summary>
    /// <remarks>
    /// A file with no version reads as this one, which is right while there is only one: a
    /// template written by hand should not have to carry a number to be read.
    /// </remarks>
    public int Version { get; set; } = Now;

    /// <summary>The version this build writes.</summary>
    public const int Now = 1;

    /// <summary>The controller, as its profile calls it, which is not the name of a port.</summary>
    public string Controller { get; set; } = "";

    /// <summary>What it is pointed at.</summary>
    public ControlTemplateTarget Target { get; set; } = new();

    /// <summary>Every control of it, in the order they were listed.</summary>
    public List<ControlTemplateControl> Controls { get; set; } = new();
}
