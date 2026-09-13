using System;
using System.Linq;
using System.Text.Json.Serialization;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Tracker.Synth.Enums;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Lighttower E=mc²: two waves drawn by hand, the thirty in between worked out, and a note that
/// goes through all of them.
/// </summary>
/// <remarks>
/// **The Fairlight CMI's way of making a sound that changes.** Its voices held thirty two
/// waveforms of 128 points and played them in turn; you drew the first and the last with the
/// light pen and had the machine merge the rest, point by point. So a wave drawn as a hollow
/// sine at the start and a jagged buzz at the end is a note that starts soft and grows teeth,
/// and nothing about it is a filter or an envelope on a fixed wave. See <see cref="IWaveSegments"/>
/// for the merge itself, which the panel draws with as well.
///
/// <see cref="Grit"/> is the half that makes it sound like the CMI rather than like a wavetable
/// synth: eight bit points read without smoothing, and a note that only moves to the next wave
/// at the end of a cycle, which is how the machine did it and is where its stepping comes from.
///
/// The two lines are held as arrays and written into a file as the words <see cref="IWaveSegments.Spell"/>
/// makes, which are a tenth of the size and can be read. A line is replaced whole rather than
/// written into, so a voice reading one on the audio thread holds either the old line or the new
/// one and never half of each.
///
/// Plain data, held by the panel and by every voice in the air, which is why a preset lands on
/// the patch that is already there rather than replacing it: see <see cref="CopyFrom"/>.
/// </remarks>
public sealed class SegmentPatch
{
    /// <summary>The merge and how a line is written, shared, since it holds nothing.</summary>
    private static readonly IWaveSegments Lines = new WaveSegments();

    /// <summary>The shortest time from the beginning to the end.</summary>
    public const double LeastSweepMs = 10;

    /// <summary>The longest, which is half a minute.</summary>
    public const double MostSweepMs = 30000;

    /// <summary>Two octaves down.</summary>
    public const double LeastTune = -24;

    /// <summary>Two octaves up.</summary>
    public const double MostTune = 24;

    /// <summary>The first wave, drawn by hand. A sine until somebody draws something else.</summary>
    [JsonIgnore]
    public double[] Begin { get; set; } = Sine();

    /// <summary>The last wave, drawn by hand. A saw until somebody draws something else.</summary>
    [JsonIgnore]
    public double[] End { get; set; } = Saw();

    /// <summary>The first wave as it is written into a file.</summary>
    /// <remarks>
    /// Words that do not read as a line leave the line that was there, which is the rule
    /// <see cref="IWaveSegments.Read"/> keeps.
    /// </remarks>
    [JsonPropertyName("Begin")]
    public string BeginWords
    {
        get => Lines.Spell(Begin);
        set => Begin = Lines.Read(value) is { Length: > 0 } read ? read : Begin;
    }

    /// <summary>The last wave as it is written into a file.</summary>
    [JsonPropertyName("End")]
    public string EndWords
    {
        get => Lines.Spell(End);
        set => End = Lines.Read(value) is { Length: > 0 } read ? read : End;
    }

    /// <summary>How long a note takes to get from the first wave to the last.</summary>
    public double SweepMs { get; set; } = 2000;

    /// <summary>What happens once it gets there.</summary>
    public SegmentMotion Motion { get; set; } = SegmentMotion.Once;

    /// <summary>
    /// Whether it sounds like the CMI: eight bit, unsmoothed, and stepping from wave to wave at the
    /// end of a cycle. Off is a smooth glide between the lines and between the points.
    /// </summary>
    public bool Grit { get; set; } = true;

    /// <summary>How long a note takes to reach full.</summary>
    public double AttackMs { get; set; } = 2;

    /// <summary>How long it takes to fall from full to the sustain level.</summary>
    public double DecayMs { get; set; } = 400;

    /// <summary>Where it holds while the key is down, nought to one.</summary>
    public double Sustain { get; set; } = 0.7;

    /// <summary>How long it takes to fall away once the key comes up.</summary>
    public double ReleaseMs { get; set; } = 300;

    /// <summary>Whole semitones added to every note.</summary>
    public double TuneSemitones { get; set; }

    /// <summary>The last hundredth of a semitone.</summary>
    public double FineCents { get; set; }

    /// <summary>How loud it comes out, before the instrument's own level.</summary>
    /// <remarks>A quarter, which leaves a drawn wave at full scale room beside everything else.</remarks>
    public double Volume { get; set; } = 0.25;

    /// <summary>A sine across the drawing, on the eight bit steps.</summary>
    public static double[] Sine() => Lines.Read(Lines.Spell(Enumerable.Range(0, WaveSegments.Points)
        .Select(point => Math.Sin(2 * Math.PI * point / WaveSegments.Points)).ToArray()));

    /// <summary>A saw across the drawing, falling from the top to the bottom, on the eight bit steps.</summary>
    public static double[] Saw() => Lines.Read(Lines.Spell(Enumerable.Range(0, WaveSegments.Points)
        .Select(point => 1 - (2.0 * point / (WaveSegments.Points - 1))).ToArray()));

    /// <summary>A copy that shares nothing, for a voice that must not feel an edit mid note.</summary>
    public SegmentPatch Clone()
    {
        var copy = new SegmentPatch();

        copy.CopyFrom(this);

        return copy;
    }

    /// <summary>
    /// Takes on another patch's settings without becoming another object, for a preset landing
    /// on the patch the panel and any sounding voice are already holding.
    /// </summary>
    /// <param name="other">Whose settings to take.</param>
    public void CopyFrom(SegmentPatch? other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Begin = (double[])(other.Begin ?? Sine()).Clone();
        End = (double[])(other.End ?? Saw()).Clone();
        SweepMs = other.SweepMs;
        Motion = other.Motion;
        Grit = other.Grit;
        AttackMs = other.AttackMs;
        DecayMs = other.DecayMs;
        Sustain = other.Sustain;
        ReleaseMs = other.ReleaseMs;
        TuneSemitones = other.TuneSemitones;
        FineCents = other.FineCents;
        Volume = other.Volume;

        Clamp();
    }

    /// <summary>
    /// Brings a patch read off disc back into range, whatever was in the file.
    /// </summary>
    /// <remarks>
    /// A line missing from the file is the line a fresh patch starts with, and one of another
    /// length is read again through the words, which stretches it across the drawing.
    /// </remarks>
    public void Clamp()
    {
        Begin = Whole(Begin, Sine);
        End = Whole(End, Saw);
        SweepMs = double.IsFinite(SweepMs) ? Math.Clamp(SweepMs, LeastSweepMs, MostSweepMs) : 2000;
        Motion = Enum.IsDefined(Motion) ? Motion : SegmentMotion.Once;
        AttackMs = double.IsFinite(AttackMs) ? Math.Clamp(AttackMs, 0, 10000) : 2;
        DecayMs = double.IsFinite(DecayMs) ? Math.Clamp(DecayMs, 0, 10000) : 400;
        Sustain = double.IsFinite(Sustain) ? Math.Clamp(Sustain, 0, 1) : 0.7;
        ReleaseMs = double.IsFinite(ReleaseMs) ? Math.Clamp(ReleaseMs, 0, 10000) : 300;
        TuneSemitones = double.IsFinite(TuneSemitones) ? Math.Clamp(TuneSemitones, LeastTune, MostTune) : 0;
        FineCents = double.IsFinite(FineCents) ? Math.Clamp(FineCents, -100, 100) : 0;
        Volume = double.IsFinite(Volume) ? Math.Clamp(Volume, 0, 2) : 0.25;
    }

    /// <summary>That line if it is the whole drawing and fit to play, or it read again until it is.</summary>
    private static double[] Whole(double[]? line, Func<double[]> fresh)
    {
        if (line is null || line.Length == 0) return fresh();

        if (line.Length == WaveSegments.Points && line.All(point => double.IsFinite(point) && point is >= -1 and <= 1))
            return line;

        return Lines.Read(Lines.Spell(line.Length == WaveSegments.Points ? line : Stretched(line)));
    }

    /// <summary>A line of another length laid across the drawing, through the words that already know how.</summary>
    private static double[] Stretched(double[] line) =>
        Lines.Read(string.Join(" ", line.Select(point =>
            Math.Round((double.IsFinite(point) ? Math.Clamp(point, -1, 1) : 0) * WaveSegments.Steps)
                .ToString(System.Globalization.CultureInfo.InvariantCulture))));
}
