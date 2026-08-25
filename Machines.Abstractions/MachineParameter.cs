namespace JingleBox2.Machines;

/// <summary>
/// One thing a machine can be set to.
/// </summary>
/// <remarks>
/// What a knob turns, what a patch stores, and what a song writes down: the same fact, said
/// once. A machine is its parameters plus what it does with them, so this is the first thing
/// that gets built when a machine is made and the thing everything else on the panel hangs off.
///
/// The key is what it is called in files and never changes; the name is what the panel says
/// and can be reworded whenever you like. Keeping those apart is what lets a machine be
/// renamed in a later version without every song that used it losing its settings.
/// </remarks>
public sealed class MachineParameter
{
    /// <summary>What it is stored under, forever. Lower case, no spaces.</summary>
    public string Key { get; set; } = "";

    /// <summary>What the panel calls it.</summary>
    public string Name { get; set; } = "";

    /// <summary>What it is measured in, if anything: dB, ms, Hz, st.</summary>
    public string Unit { get; set; } = "";

    public double Min { get; set; }

    public double Max { get; set; } = 1;

    /// <summary>Where it sits on a machine nobody has touched, and where a double click puts it back.</summary>
    public double Default { get; set; }

    /// <summary>How far one notch of the wheel or one arrow key moves it.</summary>
    public double Step { get; set; } = 0.01;

    /// <summary>
    /// Whether it is part of the sound, and so whether anything writes it down.
    /// </summary>
    /// <remarks>
    /// Almost everything on a machine is. A few things are not: how much of the wave the picture
    /// shows is a knob on the face, sits among the others and turns like them, and is no more
    /// part of the instrument than which way you happen to be looking. A preset that carried it
    /// would set somebody else's view when they loaded a sound, and a song that saved it would
    /// be a song claiming the zoom mattered.
    ///
    /// True unless the machine says otherwise, so every machine written before this reads as it
    /// always did.
    /// </remarks>
    public bool Saved { get; set; } = true;
}
