namespace JingleBox2.Machines;

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
}
