namespace JingleBox2.Machines;

/// <summary>
/// The things a panel can ask the host to do, which are not settings and never will be.
/// </summary>
/// <remarks>
/// Almost everything on a machine is a number, and a control that moves one needs nothing from
/// the host but somewhere to write it. A few are not. Taking the recording off a pad is not a
/// value it could be set to, and loading samples onto a kit opens a file dialog, reads a disc
/// and copies what it finds: none of that can live behind a knob.
///
/// Written out one by one so every action in the app can be found by searching for the string
/// that is in the machine's file, and matched by name so a machine naming one this host has
/// never heard of gets a button that does nothing rather than a panel that will not open.
/// </remarks>
public static class MachineActions
{
    /// <summary>Takes the recording off the pad in hand.</summary>
    public const string ClearPad = "clear_pad";

    /// <summary>Asks for samples from anywhere and puts them on the pads in order.</summary>
    public const string LoadPads = "load_pads";
}
