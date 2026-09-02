using System;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class VolumeScale : IVolumeScale
{
    /// <summary>What full was before the column was widened, which is FastTracker's scale.</summary>
    public const int OldMaxVolume = 64;

    /// <inheritdoc/>
    public int Widen(int volume) =>
        volume == TrackerCell.NoVolume
            ? TrackerCell.NoVolume
            : Math.Clamp(volume * (TrackerCell.MaxVolume / OldMaxVolume), 0, TrackerCell.MaxVolume);

    /// <inheritdoc/>
    public TrackerCell Widen(TrackerCell cell)
    {
        var effect = cell.Effect.Command == TrackerCommand.SetVolume
            ? cell.Effect with
            {
                Parameter = Math.Clamp(
                    cell.Effect.Parameter * (TrackerCell.MaxVolume / OldMaxVolume),
                    0,
                    TrackerCell.MaxVolume)
            }
            : cell.Effect;

        return cell with { Volume = Widen(cell.Volume), Effect = effect };
    }

    /// <inheritdoc/>
    public void Widen(Song song)
    {
        if (song == null) return;

        foreach (var pattern in song.Patterns)
        {
            for (int line = 0; line < pattern.Lines; line++)
            for (int track = 0; track < pattern.TrackCount; track++)
            for (int column = 0; column < pattern.ColumnsOn(track); column++)
            {
                var cell = pattern[line, track, column];
                if (cell.IsEmpty) continue;

                pattern[line, track, column] = Widen(cell);
            }
        }
    }
}
