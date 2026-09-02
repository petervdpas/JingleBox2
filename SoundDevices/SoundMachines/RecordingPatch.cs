using System.Text.Json;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Tracker;

namespace JingleBox2.SoundDevices.SoundMachines;

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
/// <param name="instrument">The instrument being read and written, held rather than copied.</param>
public sealed class RecordingPatch(TrackerInstrument instrument) : IPanelPatch
{
    /// <summary>The recording itself, by the name the shelf knows it under.</summary>
    /// <remarks>
    /// The keys are written out one by one rather than built from the property names, so a
    /// rename in C# cannot silently change what is in everybody's files, and so that every key
    /// in the application can be found by searching for the string that is in the file.
    /// </remarks>
    private const string TakeKey = "take";

    /// <summary>The pitch the take was recorded at, so a key can be played in tune against it.</summary>
    private const string BaseNoteKey = "baseNote";

    /// <summary>How loud it plays.</summary>
    private const string LevelKey = "level";

    /// <summary>Whether a new key cuts the one still ringing.</summary>
    private const string OneVoiceKey = "oneVoice";

    /// <summary>What a new note does to the one the track is still sounding.</summary>
    private const string NewNoteKey = "newNote";

    /// <summary>Which part of the take plays, and whether it loops.</summary>
    private const string WindowKey = "window";

    /// <summary>The envelope and filter the result passes through.</summary>
    private const string VoiceKey = "voice";

    /// <summary>How the nested objects are written, which is as small as they will go.</summary>
    /// <remarks>
    /// A settings file is read by a program and not by a person, and an instrument built on a
    /// long take is already the biggest thing in a song.
    /// </remarks>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = false };

    /// <inheritdoc/>
    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        writer.WriteString(TakeKey, instrument.FilePath);
        writer.WriteNumber(BaseNoteKey, instrument.BaseNoteSemitone);
        writer.WriteNumber(LevelKey, instrument.Volume);
        writer.WriteBoolean(OneVoiceKey, instrument.OneVoice);
        writer.WriteNumber(NewNoteKey, (int)instrument.NewNoteAction);

        writer.WritePropertyName(WindowKey);
        JsonSerializer.Serialize(writer, instrument.Shape ?? new SampleShape(), Layout);

        writer.WritePropertyName(VoiceKey);
        JsonSerializer.Serialize(writer, instrument.Patch, Layout);

        writer.WriteEndObject();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A key that is missing keeps the value the instrument already had, which is what makes a
    /// settings file written by an older machine still open: what it did not know about is
    /// simply not mentioned, and the default stands.
    ///
    /// The old loop flag that sat beside the window is kept in step with the window on the way
    /// out, so a build that predates the window still loops what it should.
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

        if (json.TryGetProperty(NewNoteKey, out var ending) && ending.TryGetInt32(out int action)
            && action >= 0 && action <= (int)Tracker.Enums.VoiceEnding.Sustain)
        {
            instrument.NewNoteAction = (Tracker.Enums.VoiceEnding)action;
        }

        if (json.TryGetProperty(WindowKey, out var window) && window.ValueKind == JsonValueKind.Object)
            instrument.Shape = window.Deserialize<SampleShape>(Layout) ?? instrument.Shape;

        if (json.TryGetProperty(VoiceKey, out var voice) && voice.ValueKind == JsonValueKind.Object)
            instrument.Patch = voice.Deserialize<Tracker.Synth.SynthPatch>(Layout) ?? instrument.Patch;

        instrument.Loop = instrument.Shape?.IsLooping ?? instrument.Loop;
    }
}
