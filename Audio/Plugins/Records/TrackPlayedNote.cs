
namespace JingleBox2.Audio.Plugins.Records;

/// <summary>
/// One note a plugin played, and which track's plugin played it.
/// </summary>
/// <param name="Track">The strip the plugin is on.</param>
/// <param name="Note">What it played.</param>
public readonly record struct TrackPlayedNote(int Track, PlayedNote Note);
