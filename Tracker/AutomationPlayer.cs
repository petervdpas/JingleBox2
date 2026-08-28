using JingleBox2.Diagnostics;
using JingleBox2.Midi;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// Writes what the lanes say, one line at a time.
/// </summary>
/// <remarks>
/// The other half of remote control, and deliberately the same half. A knob turned from CC 74
/// and the clock arriving at line 32 are one act against one interface, so this reaches a
/// parameter through <see cref="IControlTargets"/> exactly as <see cref="MidiControlRouter"/>
/// does, and everything that made a link resolve correctly makes a lane resolve correctly for
/// free: a machine only answering on a track that plays it, an insert found by what it is
/// rather than where it sits, a strip written through the fader on the screen.
///
/// It knows nothing about the clock beyond being called with a position, which is what makes it
/// testable with no audio and no window.
/// </remarks>
public sealed class AutomationPlayer
{
    private readonly IControlTargets _targets;

    /// <summary>
    /// What is known about a lane between one line and the next.
    /// </summary>
    /// <remarks>
    /// Two things, and both are about not doing work per line. The mapping is built once
    /// rather than per line, because it is the same half dozen fields every time. And the last
    /// value written is remembered so an unchanged one is not written again: a lane holding
    /// still between two points would otherwise post the same number thirty times a second, and
    /// for a plugin in another process every one of those is a round trip.
    /// </remarks>
    private sealed class Known
    {
        public ControlMapping Mapping = null!;
        public double Written = double.NaN;
        public bool Complained;
    }

    private readonly Dictionary<AutomationLane, Known> _known = new();

    public AutomationPlayer(IControlTargets targets)
    {
        _targets = targets;
    }

    /// <summary>
    /// Forgets what was written and what was resolved.
    /// </summary>
    /// <remarks>
    /// Called when playback starts and when the song changes. Both matter: the parameters have
    /// been moved by hand since the last pass, so the remembered value is a lie and would stop
    /// the first line writing anything at all, and holding lanes from a song that has been
    /// closed would keep it alive for as long as this lives.
    /// </remarks>
    public void Reset() => _known.Clear();

    /// <summary>Puts every lane on this line where it should be. Silent when there are none.</summary>
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
                // Said once per lane per pass rather than per line. A lane that names a machine
                // this track is not playing answers nothing thirty times a second, and that is
                // an ordinary thing for it to do, not thirty faults.
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

            // Compared before writing rather than trusting the target to notice. A machine
            // setting is a field and would take the write happily; a plugin parameter is a
            // message to another process.
            if (value == known.Written) continue;

            known.Written = value;
            target.Set(value);
        }
    }
}
