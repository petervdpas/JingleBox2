namespace JingleBox2.Audio.Records;

/// <summary>
/// One of the machine's audio outputs, as the settings page and the engine both see it.
/// </summary>
/// <remarks>
/// The number is BASS's own device index and means nothing outside it, which is why the two are
/// carried together: a name is the only half a person can choose from and the number is the only
/// half the library will take.
/// </remarks>
public sealed class OutputDevice
{
    /// <summary>What the audio library calls this device. Not stable across a replug.</summary>
    public int Id { get; }

    /// <summary>What the system calls it, which is what a list shows.</summary>
    public string Name { get; }

    /// <summary>One output, as the engine found it.</summary>
    public OutputDevice(int id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>The name, so a picker handed one of these shows the device rather than the type.</summary>
    public override string ToString() => Name;
}
