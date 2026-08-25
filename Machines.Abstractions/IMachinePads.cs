using System;

namespace JingleBox2.Machines;

/// <summary>
/// The kit behind a panel's pads: what each one says, which are sounding, and which is in hand.
/// </summary>
/// <remarks>
/// None of this is a setting. What a pad is called and what it plays are settings and go through
/// <see cref="IMachineValues"/> like everything else; what is here is the kit as a thing on
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

    /// <summary>Told when any of the above has moved.</summary>
    event EventHandler? Changed;
}
