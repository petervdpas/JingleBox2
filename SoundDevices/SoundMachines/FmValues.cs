using System.Collections.Generic;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Enums;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <summary>
/// Operetta's panel, wired to a real patch.
/// </summary>
/// <remarks>
/// The patch is written straight into rather than through a view model, since nothing on the
/// face is bound to one: a described panel reads and writes through here and nowhere else, and a
/// second object holding the same numbers would be a second place for them to disagree.
///
/// Every key is written out once, operator by operator, and never assembled from a number. Twenty
/// eight of them look like a pattern that wants a loop; built in one, not one of them would
/// appear in the source, and searching for <c>op3_ratio</c> in the code would find nothing while
/// the machine's own file and every preset say it.
///
/// **The algorithm is its place on the switch, from nought**, since that is what a switch on a
/// panel holds. The patch counts from one, the way the algorithms are spoken of, and the two are
/// one apart here and nowhere else.
///
/// A key it does not know reads as nought and swallows the write, so a machine file written by a
/// later version opens rather than taking the application down.
/// </remarks>
/// <param name="patch">The four operators and how they are wired.</param>
/// <param name="instrument">Whose new note action it is, which is the instrument's rather than the sound's.</param>
public sealed class FmValues(FmPatch patch, TrackerInstrument instrument) : PanelValues
{
    /// <summary>Which of the eight wirings, as a place on the switch.</summary>
    private const string AlgorithmKey = "algorithm";

    /// <summary>How much of the fourth operator is fed back into itself.</summary>
    private const string FeedbackKey = "feedback";

    /// <summary>Whole semitones added to every note.</summary>
    private const string TuneKey = "tune";

    /// <summary>And the part of a semitone.</summary>
    private const string FineKey = "fine";

    /// <summary>How loud the machine comes out.</summary>
    private const string VolumeKey = "volume";

    /// <summary>What a new note does to the one the track is still sounding.</summary>
    private const string NewNoteKey = "new_note";

    /// <summary>The first operator: the ratio.</summary>
    private const string Op1RatioKey = "op1_ratio";

    /// <summary>The first operator: the fine tuning, in cents.</summary>
    private const string Op1FineKey = "op1_fine";

    /// <summary>The first operator: the level.</summary>
    private const string Op1LevelKey = "op1_level";

    /// <summary>The first operator: the attack.</summary>
    private const string Op1AttackKey = "op1_attack";

    /// <summary>The first operator: the decay.</summary>
    private const string Op1DecayKey = "op1_decay";

    /// <summary>The first operator: the sustain.</summary>
    private const string Op1SustainKey = "op1_sustain";

    /// <summary>The first operator: the release.</summary>
    private const string Op1ReleaseKey = "op1_release";

    /// <summary>The second operator: the ratio.</summary>
    private const string Op2RatioKey = "op2_ratio";

    /// <summary>The second operator: the fine tuning, in cents.</summary>
    private const string Op2FineKey = "op2_fine";

    /// <summary>The second operator: the level.</summary>
    private const string Op2LevelKey = "op2_level";

    /// <summary>The second operator: the attack.</summary>
    private const string Op2AttackKey = "op2_attack";

    /// <summary>The second operator: the decay.</summary>
    private const string Op2DecayKey = "op2_decay";

    /// <summary>The second operator: the sustain.</summary>
    private const string Op2SustainKey = "op2_sustain";

    /// <summary>The second operator: the release.</summary>
    private const string Op2ReleaseKey = "op2_release";

    /// <summary>The third operator: the ratio.</summary>
    private const string Op3RatioKey = "op3_ratio";

    /// <summary>The third operator: the fine tuning, in cents.</summary>
    private const string Op3FineKey = "op3_fine";

    /// <summary>The third operator: the level.</summary>
    private const string Op3LevelKey = "op3_level";

    /// <summary>The third operator: the attack.</summary>
    private const string Op3AttackKey = "op3_attack";

    /// <summary>The third operator: the decay.</summary>
    private const string Op3DecayKey = "op3_decay";

    /// <summary>The third operator: the sustain.</summary>
    private const string Op3SustainKey = "op3_sustain";

    /// <summary>The third operator: the release.</summary>
    private const string Op3ReleaseKey = "op3_release";

    /// <summary>The fourth operator: the ratio.</summary>
    private const string Op4RatioKey = "op4_ratio";

    /// <summary>The fourth operator: the fine tuning, in cents.</summary>
    private const string Op4FineKey = "op4_fine";

    /// <summary>The fourth operator: the level.</summary>
    private const string Op4LevelKey = "op4_level";

    /// <summary>The fourth operator: the attack.</summary>
    private const string Op4AttackKey = "op4_attack";

    /// <summary>The fourth operator: the decay.</summary>
    private const string Op4DecayKey = "op4_decay";

    /// <summary>The fourth operator: the sustain.</summary>
    private const string Op4SustainKey = "op4_sustain";

    /// <summary>The fourth operator: the release.</summary>
    private const string Op4ReleaseKey = "op4_release";

    /// <summary>Which operator each operator key is about, and which of its seven settings.</summary>
    private static readonly Dictionary<string, (int Operator, int Setting)> Operators = new()
    {
        [Op1RatioKey] = (0, 0),
        [Op1FineKey] = (0, 1),
        [Op1LevelKey] = (0, 2),
        [Op1AttackKey] = (0, 3),
        [Op1DecayKey] = (0, 4),
        [Op1SustainKey] = (0, 5),
        [Op1ReleaseKey] = (0, 6),
        [Op2RatioKey] = (1, 0),
        [Op2FineKey] = (1, 1),
        [Op2LevelKey] = (1, 2),
        [Op2AttackKey] = (1, 3),
        [Op2DecayKey] = (1, 4),
        [Op2SustainKey] = (1, 5),
        [Op2ReleaseKey] = (1, 6),
        [Op3RatioKey] = (2, 0),
        [Op3FineKey] = (2, 1),
        [Op3LevelKey] = (2, 2),
        [Op3AttackKey] = (2, 3),
        [Op3DecayKey] = (2, 4),
        [Op3SustainKey] = (2, 5),
        [Op3ReleaseKey] = (2, 6),
        [Op4RatioKey] = (3, 0),
        [Op4FineKey] = (3, 1),
        [Op4LevelKey] = (3, 2),
        [Op4AttackKey] = (3, 3),
        [Op4DecayKey] = (3, 4),
        [Op4SustainKey] = (3, 5),
        [Op4ReleaseKey] = (3, 6),
    };

    /// <summary>The seven settings of one operator, by their place in its keys.</summary>
    private const int Ratio = 0;

    /// <inheritdoc cref="Ratio"/>
    private const int Fine = 1;

    /// <inheritdoc cref="Ratio"/>
    private const int Level = 2;

    /// <inheritdoc cref="Ratio"/>
    private const int Attack = 3;

    /// <inheritdoc cref="Ratio"/>
    private const int Decay = 4;

    /// <inheritdoc cref="Ratio"/>
    private const int Sustain = 5;

    /// <inheritdoc cref="Ratio"/>
    private const int Release = 6;

    /// <inheritdoc/>
    public override double Get(string key)
    {
        if (Operators.TryGetValue(key ?? "", out var place)) return Read(Operator(place.Operator), place.Setting);

        return key switch
        {
            AlgorithmKey => patch.Algorithm - 1,
            FeedbackKey => patch.Feedback,
            TuneKey => patch.TuneSemitones,
            FineKey => patch.FineCents,
            VolumeKey => patch.Volume,
            NewNoteKey => (double)instrument.NewNoteAction,
            _ => 0,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Something that is not a number is refused before anything is written, and what is written
    /// is brought back into range by the patch's own rules straight afterwards.
    /// </remarks>
    protected override bool Write(string key, double value)
    {
        if (!double.IsFinite(value)) return false;

        bool moved;

        if (Operators.TryGetValue(key ?? "", out var place))
        {
            var one = Operator(place.Operator);

            moved = Moved(Read(one, place.Setting), value, () => Put(one, place.Setting, value));
        }
        else
        {
            moved = key switch
            {
                AlgorithmKey => Moved(patch.Algorithm - 1, value, 0, FmPatch.Algorithms - 1,
                    at => patch.Algorithm = at + 1),
                FeedbackKey => Moved(patch.Feedback, value, () => patch.Feedback = value),
                TuneKey => Moved(patch.TuneSemitones, value, () => patch.TuneSemitones = value),
                FineKey => Moved(patch.FineCents, value, () => patch.FineCents = value),
                VolumeKey => Moved(patch.Volume, value, () => patch.Volume = value),
                NewNoteKey => Moved((int)instrument.NewNoteAction, value, 0, (int)VoiceEnding.Sustain,
                    at => instrument.NewNoteAction = (VoiceEnding)at),
                _ => false,
            };
        }

        if (moved) patch.Clamp();

        return moved;
    }

    /// <summary>That operator of the patch, after making sure it has all four.</summary>
    /// <param name="at">Which, from nought.</param>
    private FmOperator Operator(int at)
    {
        if (patch.Stack is null || patch.Stack.Count <= at) patch.Clamp();

        return patch.Stack![at];
    }

    /// <summary>One of an operator's seven settings.</summary>
    /// <param name="one">The operator.</param>
    /// <param name="setting">Which setting, by its place.</param>
    private static double Read(FmOperator one, int setting) => setting switch
    {
        Ratio => one.Ratio,
        Fine => one.FineCents,
        Level => one.Level,
        Attack => one.AttackMs,
        Decay => one.DecayMs,
        Sustain => one.Sustain,
        _ => one.ReleaseMs,
    };

    /// <summary>Writes one of an operator's seven settings.</summary>
    /// <param name="one">The operator.</param>
    /// <param name="setting">Which setting, by its place.</param>
    /// <param name="value">What to put there.</param>
    private static void Put(FmOperator one, int setting, double value)
    {
        switch (setting)
        {
            case Ratio: one.Ratio = value; break;
            case Fine: one.FineCents = value; break;
            case Level: one.Level = value; break;
            case Attack: one.AttackMs = value; break;
            case Decay: one.DecayMs = value; break;
            case Sustain: one.Sustain = value; break;
            default: one.ReleaseMs = value; break;
        }
    }
}
