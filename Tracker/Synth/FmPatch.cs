using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Operetta: four sine operators, one of eight ways of wiring them together, and feedback on the
/// fourth.
/// </summary>
/// <remarks>
/// **Frequency modulation is one sine bending another's frequency, fast.** Slowly that is
/// vibrato. At audio rates the bending is no longer heard as a wobble at all: it is heard as new
/// partials either side of the carrier, spaced by the modulator's frequency, and how many there
/// are and how loud is how hard the modulator bends. So two sines make a sound as rich as a saw
/// or as clangorous as a bell, and moving one knob moves the whole spectrum, which no filter can
/// do. That is the sound of the Yamaha DX7 and everything after it.
///
/// Four operators and eight algorithms is the shape of the DX21 and the TX81Z rather than the
/// DX7's six and thirty two, deliberately: four is enough for every classic FM sound anybody
/// reaches for, and a face with six operators on it is a page nobody can hold in their eye.
/// Operators are numbered one to four and a modulator is always numbered higher than what it
/// modulates, so the fourth is always a modulator or a plain sine, and it is the one with
/// feedback: fed back into itself a sine turns towards a saw, and then into noise.
///
/// Plain data, held by the panel and by every voice in the air, which is why a preset lands on
/// the patch that is already there rather than replacing it: see <see cref="CopyFrom"/>.
/// </remarks>
public sealed class FmPatch
{
    /// <summary>How many operators there are.</summary>
    public const int Operators = 4;

    /// <summary>How many algorithms there are, numbered from one.</summary>
    public const int Algorithms = 8;

    /// <summary>
    /// How far a modulator at full level bends what it modulates, in radians of phase.
    /// </summary>
    /// <remarks>
    /// About one and a half turns, which is past where a carrier stops sounding like a sine and
    /// short of where every sound turns to noise. What a knob is choosing is how bright, and this
    /// is how bright the top of it is.
    /// </remarks>
    public const double Depth = 3 * Math.PI;

    /// <summary>Two octaves down.</summary>
    public const double LeastTune = -24;

    /// <summary>Two octaves up.</summary>
    public const double MostTune = 24;

    /// <summary>
    /// Which operators each one is modulated by, for each algorithm, as bits: bit nought is the
    /// first operator.
    /// </summary>
    /// <remarks>
    /// Read one algorithm at a time as a row of four. The first entry is what bends the first
    /// operator, and so on; nought means it is a plain sine. Every modulator is numbered higher
    /// than what it bends, which is what lets a voice work the four out from the fourth down in
    /// one pass with every modulator already known.
    /// </remarks>
    public static readonly IReadOnlyList<int[]> ModulatedBy =
    [
        [0b0010, 0b0100, 0b1000, 0],
        [0b0010, 0b1100, 0, 0],
        [0b0110, 0, 0b1000, 0],
        [0b0010, 0, 0b1000, 0],
        [0b1000, 0b1000, 0b1000, 0],
        [0b0010, 0, 0, 0],
        [0b1110, 0, 0, 0],
        [0, 0, 0, 0],
    ];

    /// <summary>Which operators are heard, for each algorithm, as bits.</summary>
    public static readonly IReadOnlyList<int> Heard =
    [
        0b0001,
        0b0001,
        0b0001,
        0b0101,
        0b0111,
        0b1101,
        0b0001,
        0b1111,
    ];

    /// <summary>Each algorithm drawn as a line of text, in the order the face offers them.</summary>
    public static readonly IReadOnlyList<string> Drawn =
    [
        "4→3→2→1",
        "3+4→2→1",
        "2+(4→3)→1",
        "2→1, 4→3",
        "4→1+2+3",
        "2→1, 3, 4",
        "2+3+4→1",
        "1+2+3+4",
    ];

    /// <summary>Which of the eight wirings, from one.</summary>
    public int Algorithm { get; set; } = 1;

    /// <summary>How much of the fourth operator is fed back into itself, nought to one.</summary>
    public double Feedback { get; set; }

    /// <summary>Whole semitones added to every note.</summary>
    public double TuneSemitones { get; set; }

    /// <summary>The last hundredth of a semitone.</summary>
    public double FineCents { get; set; }

    /// <summary>How loud it comes out, before the instrument's own level.</summary>
    /// <remarks>
    /// A quarter by default, because four sines at full are a long way past full scale and a
    /// patch that arrives at the top leaves the mix no room for anything else.
    /// </remarks>
    public double Volume { get; set; } = 0.25;

    /// <summary>
    /// The four operators, first to fourth.
    /// </summary>
    /// <remarks>
    /// A fresh patch is the first operator heard at full and a second bending it gently at the
    /// same ratio, which is the plainest sound that is still obviously FM.
    /// </remarks>
    public List<FmOperator> Stack { get; set; } =
    [
        new() { Level = 1 },
        new() { Level = 0.35, DecayMs = 800, Sustain = 0.4 },
        new(),
        new(),
    ];

    /// <summary>A copy that shares nothing, for a voice that must not feel an edit mid note.</summary>
    public FmPatch Clone()
    {
        var copy = new FmPatch
        {
            Algorithm = Algorithm,
            Feedback = Feedback,
            TuneSemitones = TuneSemitones,
            FineCents = FineCents,
            Volume = Volume,
            Stack = new List<FmOperator>(Operators),
        };

        foreach (var one in Stack) copy.Stack.Add(one.Clone());

        copy.Clamp();

        return copy;
    }

    /// <summary>
    /// Takes on another patch's settings without becoming another object, for a preset landing
    /// on the patch the panel and any sounding voice are already holding.
    /// </summary>
    /// <param name="other">Whose settings to take.</param>
    public void CopyFrom(FmPatch? other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Algorithm = other.Algorithm;
        Feedback = other.Feedback;
        TuneSemitones = other.TuneSemitones;
        FineCents = other.FineCents;
        Volume = other.Volume;

        Clamp();

        for (int at = 0; at < Operators; at++)
            Stack[at].CopyFrom(at < other.Stack.Count ? other.Stack[at] : new FmOperator());
    }

    /// <summary>
    /// Brings a patch read off disc back into range, whatever was in the file.
    /// </summary>
    /// <remarks>
    /// Always four operators afterwards: a file holding fewer is given plain ones to make up the
    /// count, and one holding more has the rest dropped, since there is nowhere for them to go.
    /// </remarks>
    public void Clamp()
    {
        Algorithm = Math.Clamp(Algorithm, 1, Algorithms);
        Feedback = double.IsFinite(Feedback) ? Math.Clamp(Feedback, 0, 1) : 0;
        TuneSemitones = double.IsFinite(TuneSemitones) ? Math.Clamp(TuneSemitones, LeastTune, MostTune) : 0;
        FineCents = double.IsFinite(FineCents) ? Math.Clamp(FineCents, -100, 100) : 0;
        Volume = double.IsFinite(Volume) ? Math.Clamp(Volume, 0, 2) : 0.25;

        Stack ??= [];
        Stack.RemoveAll(one => one is null);

        while (Stack.Count < Operators) Stack.Add(new FmOperator());

        if (Stack.Count > Operators) Stack.RemoveRange(Operators, Stack.Count - Operators);

        foreach (var one in Stack) one.Clamp();
    }
}
