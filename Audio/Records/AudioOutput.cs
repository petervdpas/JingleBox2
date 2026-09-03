using JingleBox2.Audio.Enums;

namespace JingleBox2.Audio.Records;

/// <summary>
/// One of the machine's audio outputs, as the settings page and the engine both see it.
/// </summary>
/// <remarks>
/// The number means nothing outside the list it came from, which is why the two are carried
/// together: a name is the only half a person can choose from and the number is the only half a
/// library will take.
///
/// There are two such lists, the system's endpoints and the ASIO drivers, and both start at
/// nought. <see cref="Id"/> is the one number that names a device out of either, composed by
/// <c>JingleBox2.Audio.Interfaces.IAudioOutputs</c>; <see cref="Kind"/> is the same fact said in
/// words, so a page can mark the ASIO ones without doing arithmetic on an id.
/// </remarks>
public sealed class AudioOutput
{
    /// <summary>The one number that names this device. Not stable across a replug.</summary>
    public int Id { get; }

    /// <summary>What it is called, which is the only half a person can choose from.</summary>
    public string Name { get; }

    /// <summary>Which of the two lists it came out of.</summary>
    public AudioOutputKind Kind { get; }

    /// <summary>One device, by its number and its name.</summary>
    /// <param name="id">The one number that names it.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="kind">Which list it came out of. Left out, the system's.</param>
    public AudioOutput(int id, string name, AudioOutputKind kind = AudioOutputKind.System)
    {
        Id = id;
        Name = name;
        Kind = kind;
    }

    /// <summary>
    /// The name, with ASIO said out loud.
    /// </summary>
    /// <remarks>
    /// A picker handed one of these shows this, and the two lists sit in it together. An ASIO
    /// driver is often named after the same card as a system endpoint, so without the word there
    /// are two identical lines that behave completely differently.
    /// </remarks>
    public override string ToString() =>
        Kind == AudioOutputKind.Asio ? Name + "  (ASIO)" : Name;
}
