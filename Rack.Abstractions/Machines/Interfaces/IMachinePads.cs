using System;

namespace JingleBox2.Rack.Machines.Interfaces;

/// <summary>
/// The kit behind a panel's pads: what each one says, which are sounding, and which is in hand.
/// </summary>
/// <remarks>
/// None of this is a setting. What a pad is called and what it plays are settings and go through
/// <see cref="JingleBox2.Rack.Faces.Interfaces.IPanelValues"/> like everything else; what is here is the kit as a thing on
/// screen. Which pad is in hand is not saved anywhere and should not be: it is where somebody's
/// attention is, and a song that remembered it would be a song claiming that mattered. Which
/// pads are lit is what the machine is doing this instant.
///
/// Asked a pad at a time rather than handed a list, so that whoever supplies it can keep the kit
/// in whatever shape it already has. Sixteen calls to draw a grid is nothing, and a list would
/// mean a second copy of the kit to keep in step with the first.
///
/// <see cref="Changed"/> rather than a redraw, because these move while nothing else does. A
/// crash rings on under the snare that follows it and both pads are lit at once; rebuilding the
/// panel for that would blink every knob on the machine forty times a second.
/// </remarks>
public interface IMachinePads
{
    /// <summary>How many there are. The panel says how they are arranged, this says how many.</summary>
    int Count { get; }

    /// <summary>What is written on that pad: its name, or what it is playing, or nothing.</summary>
    string Cap(int at);

    /// <summary>The key that fires it, in the wording the rest of the app uses for a note.</summary>
    string Note(int at);

    /// <summary>Whether it is sounding this instant.</summary>
    bool Lit(int at);

    /// <summary>Whether anything is on it, so an empty pad can be drawn as an empty pad.</summary>
    bool Filled(int at);

    /// <summary>Which one the controls beside the grid are about. Written when one is pressed.</summary>
    int Picked { get; set; }

    /// <summary>Hits it, which is what pressing a pad on a drum machine has always done.</summary>
    void Hit(int at);

    /// <summary>
    /// A hand has gone down on it, and has not come up yet.
    /// </summary>
    /// <remarks>
    /// A pad has two halves like any other key, and for a long time this contract only had the
    /// one: <see cref="Hit"/>, which is the moment it sounds. So a pad hit lit the pad and
    /// sounded the note and left the drawn keyboard dark, while clicking the very same note on
    /// that keyboard lit it. The two are one act and should look like one.
    ///
    /// This half is the light and nothing else. What sounds a pad is still <see cref="Hit"/>,
    /// on the way back up, so sliding off a pad is still how you change your mind about a press
    /// you have begun: the key lights while the hand is down and goes out again with nothing
    /// having sounded.
    /// </remarks>
    ///
    /// <remarks>
    /// Answered with nothing by default, and that is not laziness. This contract is published:
    /// an outside machine implements it, so a member added without one is every existing
    /// machine refusing to compile against the next version over a light. A kit that says
    /// nothing here sounds and lights its own pads exactly as it did, and only the drawn
    /// keyboard is quieter than it could be.
    /// </remarks>
    void Held(int at) { }

    /// <summary>The hand has come up, so whatever <see cref="Held"/> lit goes out.</summary>
    /// <remarks>Nothing by default, for the reason <see cref="Held"/> gives.</remarks>
    void Let(int at) { }

    /// <summary>Told when any of the above has moved.</summary>
    event EventHandler? Changed;
}
