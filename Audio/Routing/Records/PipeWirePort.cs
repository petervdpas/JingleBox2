
namespace JingleBox2.Audio.Routing.Records;

/// <summary>One end of a connection: a node, and one port on it.</summary>
/// <param name="Node">The node's name, which is the program or the device the audio belongs to.</param>
/// <param name="Port">
/// The port's own name. The channel is in it, which is what pairs a source with the capture:
/// see <see cref="PipeWireGraph.Channel"/>.
/// </param>
public readonly record struct PipeWirePort(string Node, string Port);
