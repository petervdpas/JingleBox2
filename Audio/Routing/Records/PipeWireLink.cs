
namespace JingleBox2.Audio.Routing.Records;

/// <summary>One connection in the graph, in the direction the audio actually travels.</summary>
/// <param name="From">The port giving the audio.</param>
/// <param name="To">The port taking it.</param>
public readonly record struct PipeWireLink(PipeWirePort From, PipeWirePort To);
