using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi.Records;

/// <summary>
/// One thing on a track that could be pointed at, ready to be put in a list.
/// </summary>
/// <remarks>
/// The device is the heading and the name is the row. Apart rather than joined, because a list
/// gathered under its devices is the only shape in which forty parameters can be read: joined,
/// every row would begin with the same word for as long as one device's parameters ran.
/// </remarks>
/// <param name="Mapping">What to ask <see cref="IControlTargets.Find"/> for.</param>
/// <param name="Device">What holds it: a machine, a plugin, or the mixer.</param>
/// <param name="Name">What the parameter is called on its own face.</param>
/// <param name="Unit">What it is measured in, when the thing said. Empty otherwise.</param>
public sealed record ControlChoice(ControlMapping Mapping, string Device, string Name, string Unit = "");
