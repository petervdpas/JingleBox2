using System;
using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// A lane holds its values from nought to one across the tempo's range, so a tempo is put in as
/// its share of that range. The point on the line before is only added where there is not one
/// already, so a shape somebody drew is not moved.
/// </remarks>
public sealed class TempoSteps : ITempoSteps
{
    /// <inheritdoc/>
    public void Step(Song song, int line, double bpm)
    {
        if (song is null || line < 0 || line >= song.TotalLines || double.IsNaN(bpm)) return;

        double songBpm = song.Timing.ClampedBpm;

        var mapping = new ControlMapping
        {
            Kind = ControlKind.Mix,
            Mix = MixControl.Tempo,
            Scope = ControlScope.Fixed,
            Track = TrackerPlayer.MasterStrip
        };

        var lane = song.Tempo ?? song.Lane(AutomationLane.For(mapping, TrackerPlayer.MasterStrip)!);

        if (line > 0)
        {
            if (lane.Points.Count == 0) lane.Put(0, Share(songBpm));

            double before = lane.ValueAt(line - 1) ?? Share(songBpm);

            if (!lane.Points.Any(point => point.Time == line - 1)) lane.Put(line - 1, before);
        }

        lane.Put(line, Share(bpm));
    }

    /// <summary>A tempo as its share of the range a song allows, which is how a lane holds it.</summary>
    private static double Share(double bpm) =>
        (Math.Clamp(bpm, TrackerTiming.MinBpm, TrackerTiming.MaxBpm) - TrackerTiming.MinBpm)
        / (TrackerTiming.MaxBpm - TrackerTiming.MinBpm);
}
