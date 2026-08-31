namespace JingleBox2.Midi;

/// <summary>
/// What a template is a template for: a machine, an effect, a mixer strip or the transport.
/// </summary>
/// <remarks>
/// The id is what decides and the name is what a person reads. Both are written because a
/// machine whose id this installation does not have should still be able to say whose template
/// this is, which is the difference between "nothing happened" and "you have not got OddSkilla".
/// </remarks>
public sealed class ControlTemplateTarget
{
    /// <summary>One of machine, effect, mixer or transport.</summary>
    public string Kind { get; set; } = "";

    /// <summary>
    /// Which one: a machine's id, a plugin's id, or a strip.
    /// </summary>
    /// <remarks>
    /// A strip is written the way the mixer says it, so the master is the word master and a
    /// track is its number counting from one. Empty for the transport, which is one thing.
    /// </remarks>
    public string Id { get; set; } = "";

    /// <summary>What it is called, for reading and for saying what is missing.</summary>
    public string Name { get; set; } = "";
}
