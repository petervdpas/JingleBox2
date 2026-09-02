using System.Text.Json;

namespace JingleBox2.Rack.SoundDevices.Faces.Interfaces;

/// <summary>
/// The settings of one instrument made on a machine, as the machine keeps them.
/// </summary>
/// <remarks>
/// A machine's own business, and nobody else's. The host stores it, hands it back and never
/// looks inside: what a Zampler keeps is a map of zones and what OddSkilla keeps is a wave and
/// an envelope, and neither is anything the tracker needs to understand to play a note.
///
/// Read and written as JSON because a song is a file somebody can open, copy and mail, and
/// because a machine that arrives later has to be able to read what a version of itself wrote
/// last year. A machine that meets a key it does not know leaves it alone; a machine that
/// misses one it expected uses its default. That rule is what makes a settings file survive
/// both the machine and the host moving on.
/// </remarks>
public interface IPanelPatch
{
    /// <summary>
    /// Takes what was written down, and keeps its defaults for whatever is missing.
    /// </summary>
    void Read(JsonElement json);

    /// <summary>Writes everything it would need to be itself again.</summary>
    void Write(Utf8JsonWriter writer);
}
