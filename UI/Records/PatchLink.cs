namespace JingleBox2.UI.Records;

/// <summary>A cable: one output port joined to one input port.</summary>
/// <remarks>
/// One cable however many channels it carries, rather than one per channel. What a stereo pair
/// really is on the machine underneath is two wires, and drawing it as two cables you can pull
/// apart would offer a state nothing in this application can express: half a source connected.
/// How the channels line up is <see cref="Interfaces.IPatchWiring.Pairs"/>, in one place, so the
/// picture and whatever does the wiring cannot disagree about it.
/// </remarks>
/// <param name="From">Where the audio comes from, which is always an output.</param>
/// <param name="To">Where it goes, which is always an input.</param>
public readonly record struct PatchLink(PatchPort From, PatchPort To);
