using JingleBox2.Machines;

namespace JingleBox2.Machines.Interfaces;

/// <summary>
/// Where a panel reads and writes the values its controls stand for.
/// </summary>
/// <remarks>
/// A panel is a picture of somebody's settings, and whose settings they are depends on where
/// the panel is standing: in a song it is the instrument's, in the machine editor it is a
/// preview nobody keeps. Neither is any business of the drawing, so the panel is handed this
/// and asks it.
///
/// Values are doubles throughout, switches included, so that a machine's settings are one kind
/// of thing however they are shown.
///
/// Not quite all of them. Which recording a sampler plays is a name, and a name is not a number
/// however hard it is squeezed, so text settings sit beside the values rather than among them.
/// They are a second pair of methods and not a second interface, because it is one machine's
/// settings either way and whoever holds them holds both.
/// </remarks>
public interface IMachineValues
{
    /// <summary>What that parameter is set to, or its default when nothing has set it.</summary>
    double Get(string key);

    /// <summary>Sets it, because somebody turned the control that stands for it.</summary>
    void Set(string key, double value);

    /// <summary>What that text setting says, or nothing when it has never been set.</summary>
    /// <remarks>
    /// Given a body so that everything holding a machine's settings today still compiles: most
    /// machines are numbers from end to end and have no text to answer with. Nothing is the
    /// right answer for those, and the controls that read text already draw an empty setting as
    /// an invitation to fill it.
    /// </remarks>
    string GetText(string key) => "";

    /// <summary>Sets it, because somebody picked something the machine names rather than counts.</summary>
    void SetText(string key, string value) { }

    /// <summary>
    /// Raised when something in here moved, saying which, for anything showing these.
    /// </summary>
    /// <remarks>
    /// Not the same relationship as the one who owns the values, who is told through their own
    /// callback because they have work to do about it: mark the song dirty, save the patch. This
    /// is for a picture of the settings, and there can be several of those at once.
    ///
    /// It exists because a panel had no way of hearing about a value it did not write. A knob
    /// turned with the mouse goes through the panel, which knows perfectly well what it just
    /// did. A knob turned on a controller writes the value directly and the panel was never
    /// told, so the drawing sat on the old number until something unrelated happened to make it
    /// read itself again. From the outside that is a controller with a lag of anywhere between
    /// a second and for ever.
    ///
    /// It says which setting moved, which a panel does not need and a history does: a knob being
    /// dragged is one edit and forty messages, and gathering those into one step means knowing
    /// they were all the same control.
    ///
    /// Given a body so that anything holding a machine's settings today still compiles. Not
    /// raising it costs the drawing and nothing else.
    /// </remarks>
    event System.Action<string>? Said { add { } remove { } }
}
