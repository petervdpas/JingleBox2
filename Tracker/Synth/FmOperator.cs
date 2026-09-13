using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One of Operetta's four operators: a sine at some ratio of the note, how loud it is, and the
/// envelope that shapes that loudness over the note.
/// </summary>
/// <remarks>
/// An operator is the same thing whether it is heard or not. Which of the four reach the output
/// and which only move another one's frequency is the algorithm's to say, not the operator's, so
/// the level here is how loud it is when it is a carrier and how far it bends another when it is
/// a modulator. That is why a modulator's envelope is the one that makes FM worth having: a
/// modulator falling away over a second is a sound starting bright and ending pure, which is a
/// bell, an electric piano or a plucked bass depending on the ratios.
///
/// Plain data, held by the patch and read by every voice sounding it.
/// </remarks>
public sealed class FmOperator
{
    /// <summary>The lowest ratio, which is an octave under the note.</summary>
    public const double LeastRatio = 0.5;

    /// <summary>The highest, which is four octaves over it.</summary>
    public const double MostRatio = 16;

    /// <summary>How far the fine tuning goes either way, in cents.</summary>
    public const double MostFineCents = 100;

    /// <summary>The longest attack, in milliseconds.</summary>
    public const double MostAttackMs = 5000;

    /// <summary>The longest decay and the longest release.</summary>
    public const double MostDecayMs = 10000;

    /// <summary>
    /// How many times the note's frequency this operator runs at.
    /// </summary>
    /// <remarks>
    /// Whole numbers are harmonic: a modulator at two over a carrier at one adds the harmonics a
    /// saw has. Anything else is not, and a ratio like 3.5 or 1.41 is where bells and metal come
    /// from.
    /// </remarks>
    public double Ratio { get; set; } = 1;

    /// <summary>A few cents off the ratio, which on a carrier beside another is a slow beating.</summary>
    public double FineCents { get; set; }

    /// <summary>How loud it is as a carrier, or how hard it bends another as a modulator, nought to one.</summary>
    public double Level { get; set; }

    /// <summary>How long it takes to reach its level.</summary>
    public double AttackMs { get; set; } = 2;

    /// <summary>How long it takes to fall from there to its sustain.</summary>
    public double DecayMs { get; set; } = 400;

    /// <summary>Where it holds while the key is down, as a share of its level.</summary>
    public double Sustain { get; set; } = 1;

    /// <summary>How long it takes to fall away once the key comes up.</summary>
    public double ReleaseMs { get; set; } = 300;

    /// <summary>A copy that shares nothing.</summary>
    public FmOperator Clone() => new()
    {
        Ratio = Ratio,
        FineCents = FineCents,
        Level = Level,
        AttackMs = AttackMs,
        DecayMs = DecayMs,
        Sustain = Sustain,
        ReleaseMs = ReleaseMs,
    };

    /// <summary>Takes on another operator's settings without becoming another object.</summary>
    /// <param name="other">Whose settings to take.</param>
    public void CopyFrom(FmOperator? other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Ratio = other.Ratio;
        FineCents = other.FineCents;
        Level = other.Level;
        AttackMs = other.AttackMs;
        DecayMs = other.DecayMs;
        Sustain = other.Sustain;
        ReleaseMs = other.ReleaseMs;

        Clamp();
    }

    /// <summary>Brings every setting back into range, whatever was in the file.</summary>
    public void Clamp()
    {
        Ratio = Held(Ratio, LeastRatio, MostRatio, 1);
        FineCents = Held(FineCents, -MostFineCents, MostFineCents, 0);
        Level = Held(Level, 0, 1, 0);
        AttackMs = Held(AttackMs, 0, MostAttackMs, 2);
        DecayMs = Held(DecayMs, 0, MostDecayMs, 400);
        Sustain = Held(Sustain, 0, 1, 1);
        ReleaseMs = Held(ReleaseMs, 0, MostDecayMs, 300);
    }

    /// <summary>That value inside its ends, or what it starts at where it is not a number.</summary>
    private static double Held(double value, double low, double high, double otherwise) =>
        double.IsFinite(value) ? Math.Clamp(value, low, high) : otherwise;
}
