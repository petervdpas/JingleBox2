using JingleBox2.Diagnostics;
using JingleBox2.Midi;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// Keeps what it has worked out about each lane between one line and the next, which is what
/// stops the per-line work being a mapping built from scratch and a write nobody needed.
/// </remarks>
public sealed class AutomationPlayer : IAutomationPlayer
{
    /// <summary>Where a parameter is reached, which is the same door remote control goes through.</summary>
    private readonly IControlTargets _targets;

    /// <summary>
    /// What is known about a lane between one line and the next.
    /// </summary>
    /// <remarks>
    /// Three things, and all of them are about not doing work per line. The mapping is built once
    /// rather than per line, because it is the same half dozen fields every time. The last value
    /// written is remembered so an unchanged one is not written again. And whether the log has
    /// already been told this lane resolves to nothing is remembered too, so it is said once per
    /// pass rather than thirty times a second.
    /// </remarks>
    private sealed class Known
    {
        /// <summary>The lane as a mapping, built the first time this lane is played.</summary>
        public ControlMapping Mapping = null!;

        /// <summary>The last value written, in the parameter's own units. NaN before the first.</summary>
        public double Written = double.NaN;

        /// <summary>Whether the log has already been told this lane reaches nothing.</summary>
        public bool Complained;
    }

    /// <summary>What is known about each lane, emptied by <see cref="Reset"/>.</summary>
    /// <remarks>
    /// Keyed by the lane itself, so a lane taken out of a pattern takes its entry with it the
    /// next time the song changes and nothing here holds a closed song alive.
    /// </remarks>
    private readonly Dictionary<AutomationLane, Known> _known = new();

    /// <summary>Reads and writes parameters through the door a link already goes through.</summary>
    public AutomationPlayer(IControlTargets targets)
    {
        _targets = targets;
    }

    /// <inheritdoc/>
    public void Reset() => _known.Clear();

    /// <inheritdoc/>
    public void Play(Song? song, TrackerPosition position)
    {
        var pattern = song?.PatternAt(position.OrderIndex);
        if (pattern is null || pattern.Lanes.Count == 0) return;
        if (position.Line < 0 || position.Line >= pattern.Lines) return;

        foreach (var lane in pattern.Lanes)
        {
            if (lane.ValueAt(position.Line) is not double wanted) continue;

            if (!_known.TryGetValue(lane, out var known))
            {
                known = new Known { Mapping = lane.Mapping() };
                _known[lane] = known;
            }

            var target = _targets.Find(known.Mapping);
            if (target is null)
            {
                if (!known.Complained && Log.On(LogArea.Tracker))
                {
                    known.Complained = true;
                    Log.Write(LogArea.Tracker, () =>
                        "automation: track " + (lane.Track + 1) + " has a lane for "
                        + lane.Kind + " '" + (lane.Key.Length > 0 ? lane.Key : lane.Mix.ToString())
                        + "' and nothing here answers to it");
                }

                continue;
            }

            double value = target.Min + wanted * (target.Max - target.Min);

            if (value == known.Written) continue;

            known.Written = value;
            target.Set(value);
        }
    }
}
