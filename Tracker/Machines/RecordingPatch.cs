using JingleBox2.Machines;
using System.Text.Json;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// What the Recording machine keeps: one take, the window played out of it, and the voice it
/// goes through.
/// </summary>
/// <remarks>
/// The first machine written to the contract, and it says out loud what has until now been
/// spread across <see cref="TrackerInstrument"/>'s fields: the file, the pitch it was recorded
/// at, which part of it plays, whether a key cuts the last one, and the envelope and filter the
/// result passes through. Nothing else on that class is any of this machine's business.
///
/// It reads and writes the instrument it is given rather than holding a copy. That is on
/// purpose while the two live side by side: the panel, the player and the file format still
/// speak to the instrument, so a settings object with its own copy of everything would be a
/// second truth to keep in step. When the format moves over, this is the thing it moves to,
/// and the fields go with it.
/// </remarks>
public sealed class RecordingPatch(TrackerInstrument instrument) : IMachinePatch
{
    // Written out, not built from the property names, so a rename in C# cannot silently
    // change what is in everybody's files.
    private const string TakeKey = "take";
    private const string BaseNoteKey = "baseNote";
    private const string LevelKey = "level";
    private const string OneVoiceKey = "oneVoice";
    private const string WindowKey = "window";
    private const string VoiceKey = "voice";

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = false };

    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        writer.WriteString(TakeKey, instrument.FilePath);
        writer.WriteNumber(BaseNoteKey, instrument.BaseNoteSemitone);
        writer.WriteNumber(LevelKey, instrument.Volume);
        writer.WriteBoolean(OneVoiceKey, instrument.OneVoice);

        writer.WritePropertyName(WindowKey);
        JsonSerializer.Serialize(writer, instrument.Shape ?? new SampleShape(), Layout);

        writer.WritePropertyName(VoiceKey);
        JsonSerializer.Serialize(writer, instrument.Patch, Layout);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Takes what is there and leaves the rest alone.
    /// </summary>
    /// <remarks>
    /// A key that is missing keeps the value the instrument already had, which is what makes a
    /// settings file written by an older machine still open: what it did not know about is
    /// simply not mentioned, and the default stands.
    /// </remarks>
    public void Read(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object) return;

        if (json.TryGetProperty(TakeKey, out var take) && take.ValueKind == JsonValueKind.String)
            instrument.FilePath = take.GetString() ?? "";

        if (json.TryGetProperty(BaseNoteKey, out var note) && note.TryGetInt32(out int semitone))
            instrument.BaseNoteSemitone = semitone;

        if (json.TryGetProperty(LevelKey, out var level) && level.TryGetDouble(out double volume))
            instrument.Volume = volume;

        if (json.TryGetProperty(OneVoiceKey, out var one) &&
            (one.ValueKind == JsonValueKind.True || one.ValueKind == JsonValueKind.False))
        {
            instrument.OneVoice = one.GetBoolean();
        }

        if (json.TryGetProperty(WindowKey, out var window) && window.ValueKind == JsonValueKind.Object)
            instrument.Shape = window.Deserialize<SampleShape>(Layout) ?? instrument.Shape;

        if (json.TryGetProperty(VoiceKey, out var voice) && voice.ValueKind == JsonValueKind.Object)
            instrument.Patch = voice.Deserialize<Synth.SynthPatch>(Layout) ?? instrument.Patch;

        // The old flag beside the window, kept in step so a build that predates the window
        // still loops.
        instrument.Loop = instrument.Shape?.IsLooping ?? instrument.Loop;
    }
}
