using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// The signal is split into three bands with one-pole filters, which is as coarse as the question
/// is: nobody needs a spectrum to hear that a hat is top. The bands are followed a few milliseconds
/// at a time, in decibels, so a quiet hit rising out of silence and a loud one rising out of a
/// tail are judged by the same rule.
///
/// Every number the rules turn on is a constant below with the reason written beside it, so a
/// recording judged wrongly is a matter of which constant, not of which line.
/// </remarks>
public sealed class DrumListener : IDrumListener
{
    /// <summary>Where the bottom band stops: under this is a kick's weight and a tom's body.</summary>
    private const double LowHz = 150;

    /// <summary>Where the top band starts: over this is noise, sticks and metal.</summary>
    private const double HighHz = 4000;

    /// <summary>How long one step of following the bands is.</summary>
    private const double StepSeconds = 0.005;

    /// <summary>How far a band has to jump above where it just was to be a hit, in decibels.</summary>
    /// <remarks>
    /// Where it just was is the loudest of the few steps before, so a band already falling does
    /// not count its own wobble. Nine decibels is a sound becoming about three times as loud in a
    /// few milliseconds, which a struck drum does and a tail does not.
    /// </remarks>
    private const double RiseDecibels = 9;

    /// <summary>How many steps back where a band just was is looked for.</summary>
    /// <remarks>
    /// At the very start of a recording there is nothing before, which counts as silence, so a
    /// recording that begins on a hit has that hit found.
    /// </remarks>
    private const int LookBack = 3;

    /// <summary>
    /// How close two hits may be and still be two. Closer than this and they are one hit, the
    /// stronger of the two: a flam is one sound, and no drummer plays two apart in less.
    /// </summary>
    private const double ApartSeconds = 0.045;

    /// <summary>How far under the recording's loudest moment a band has to be before nothing in it counts.</summary>
    private const double FloorDecibels = -50;

    /// <summary>How long after a hit begins its loudest moment is looked for.</summary>
    private const double AttackSeconds = 0.02;

    /// <summary>How much of a hit is listened to for the share between the bands.</summary>
    /// <remarks>
    /// The first moments, since that is where a drum is itself: a kick's click and body, a
    /// snare's crack. Later the room and the next hit have more to say than the drum.
    /// </remarks>
    private const double FeatureSeconds = 0.06;

    /// <summary>How long before a hit what was already ringing is measured, to be taken away from it.</summary>
    /// <remarks>
    /// A hat on a kick's tail would otherwise be heard as mostly kick. What the hit adds is what it
    /// is, so the bands are judged on how much they grew, not on how much is in them.
    /// </remarks>
    private const double BeforeSeconds = 0.02;

    /// <summary>How much of a hit is listened to for how noisy it is.</summary>
    private const double NoiseSeconds = 0.04;

    /// <summary>How far a hit falls from its loudest to count as having died away, in decibels.</summary>
    private const double DecayDecibels = -20;

    /// <summary>How far it falls to count as gone, which is where its pad stops.</summary>
    private const double GoneDecibels = -40;

    /// <summary>The longest a pad is ever given, whatever is still ringing.</summary>
    private const double LongestSeconds = 1.5;

    /// <summary>A kick has at least this share of its growth in the bottom band, and little noise.</summary>
    /// <remarks>
    /// Not most of it: a kick's click and the upper half of its body are above a hundred and fifty
    /// cycles, and a gentle filter lets half a kick through into the middle band. What tells it
    /// from a snare with a deep body is the noise, <see cref="KickCrossings"/>.
    /// </remarks>
    private const double KickLow = 0.35;

    /// <summary>A kick crosses nought fewer times a second than this: it is a tone, not a rattle.</summary>
    private const double KickCrossings = 2000;

    /// <summary>Noise over this, with a share of top at least <see cref="MetalShare"/>, is metal: sticks on a hat or a cymbal.</summary>
    /// <remarks>
    /// A hat played over another drum lends half its top to the middle band on the way down, so
    /// a share of top alone calls it a snare. Its noise does not change: a hat crosses nought ten
    /// thousand times a second whatever is under it, and a snare a quarter of that.
    /// </remarks>
    private const double MetalCrossings = 7000;

    /// <inheritdoc cref="MetalCrossings"/>
    private const double MetalShare = 0.3;

    /// <summary>How far past nought a swing has to go to be counted, as a share of the loudest moment.</summary>
    private const double SwingShare = 0.05;

    /// <summary>A hat or a cymbal has at least this share in the top band.</summary>
    private const double MetalHigh = 0.55;

    /// <summary>A closed hat has died away within this; an open one within <see cref="OpenHatSeconds"/>.</summary>
    private const double ClosedHatSeconds = 0.15;

    /// <summary>Past this, top that rings is a cymbal rather than a hat.</summary>
    private const double OpenHatSeconds = 0.7;

    /// <summary>Crossings of nought a second, over which a hit that is not a kick or metal is a snare rather than a tone.</summary>
    private const double NoisyCrossings = 1800;

    /// <summary>A hit mostly in the middle that has died away sooner than this is a snare; later, a tom.</summary>
    /// <remarks>
    /// A snare in a produced beat is often filtered until it has no rattle left to count, and what
    /// is left is a short knock in the middle band. What a tom has that such a snare has not is
    /// ring: it is a tuned drum and it sings. Measured on a clean loop, its snares had fallen
    /// twenty decibels within a tenth to a fifth of a second.
    /// </remarks>
    private const double TomSeconds = 0.25;

    /// <summary>
    /// A hit quieter than this share of the loudest one is not given a pad.
    /// </summary>
    /// <remarks>
    /// Ghost notes and the room ringing between hits are found as hits, rightly, since they are
    /// in the recording; but a pad of the room is not a drum anybody wanted. Measured on a clean
    /// loop, the hats that were meant came in at a tenth of the loudest snare and the ringing at
    /// a thirtieth.
    /// </remarks>
    private const double QuietestShare = 0.08;

    /// <summary>How near two hits of one sound have to be to be the same drum.</summary>
    /// <remarks>
    /// In the space of the three shares, how long it rings and how noisy it is. Noise counts for
    /// least: a hat played over a kick crosses nought half as often as the same hat played alone,
    /// since the kick is under it, and the two are one drum.
    /// </remarks>
    private const double SameDrum = 0.4;

    /// <summary>How long a hit has to ring clear of the next for more to make no difference to which is kept.</summary>
    private const double ClearEnough = 0.5;

    /// <inheritdoc/>
    public IReadOnlyList<DrumHit> Listen(SampleData? sample)
    {
        if (sample is null || sample.IsEmpty || sample.SampleRate <= 0) return Array.Empty<DrumHit>();

        int rate = sample.SampleRate;
        int count = (int)Math.Min(sample.FrameCount, int.MaxValue);

        var whole = new double[count];
        var low = new double[count];
        var middle = new double[count];
        var high = new double[count];

        Split(sample, whole, low, middle, high);

        int step = Math.Max(1, (int)Math.Round(StepSeconds * rate));
        int steps = count / step;

        if (steps < 2) return Array.Empty<DrumHit>();

        var all = Energy(whole, step, steps);
        var bands = new[] { Energy(low, step, steps), Energy(middle, step, steps), Energy(high, step, steps) };

        double loudest = all.Max();

        if (loudest <= 0) return Array.Empty<DrumHit>();

        var starts = Onsets(bands, all, loudest, (int)Math.Round(ApartSeconds * rate / step));

        var hits = new List<DrumHit>(starts.Count);

        for (int at = 0; at < starts.Count; at++)
        {
            int from = starts[at];
            int next = at + 1 < starts.Count ? starts[at + 1] : steps;

            hits.Add(Judge(whole, low, middle, high, all, step, rate, from, next, count));
        }

        return hits;
    }

    /// <summary>Folds the recording to one side and splits it into the three bands.</summary>
    private static void Split(SampleData sample, double[] whole, double[] low, double[] middle, double[] high)
    {
        double rate = sample.SampleRate;
        double lowPole = 1 - Math.Exp(-2 * Math.PI * LowHz / rate);
        double highPole = 1 - Math.Exp(-2 * Math.PI * HighHz / rate);
        double first = 0, second = 0, under = 0;

        for (int frame = 0; frame < whole.Length; frame++)
        {
            double sum = 0;

            for (int channel = 0; channel < sample.Channels; channel++) sum += sample.At(frame, channel);

            double x = sum / sample.Channels;

            first += (x - first) * lowPole;
            second += (first - second) * lowPole;
            under += (x - under) * highPole;

            whole[frame] = x;
            low[frame] = second;
            high[frame] = x - under;
            middle[frame] = under - second;
        }
    }

    /// <summary>The mean square of a signal over each step.</summary>
    private static double[] Energy(double[] signal, int step, int steps)
    {
        var energy = new double[steps];

        for (int at = 0; at < steps; at++)
        {
            double sum = 0;

            for (int i = at * step; i < (at + 1) * step; i++) sum += signal[i] * signal[i];

            energy[at] = sum / step;
        }

        return energy;
    }

    /// <summary>Decibels, with nothing at all reading as very quiet rather than as minus infinity.</summary>
    private static double Decibels(double energy) => 10 * Math.Log10(energy + 1e-12);

    /// <summary>
    /// Where the hits begin, in steps: wherever a band jumps well above where it just was, the
    /// stronger of any two too close together.
    /// </summary>
    private static List<int> Onsets(double[][] bands, double[] all, double loudest, int apart)
    {
        int steps = all.Length;
        var rise = new double[steps];

        foreach (var band in bands)
        {
            double floor = band.Max() * Math.Pow(10, FloorDecibels / 10);

            for (int at = 0; at < steps; at++)
            {
                if (band[at] <= floor) continue;

                double before = 0;

                for (int back = 1; back <= LookBack && at - back >= 0; back++) before = Math.Max(before, band[at - back]);

                rise[at] = Math.Max(rise[at], Decibels(band[at]) - Decibels(before));
            }
        }

        double quiet = loudest * Math.Pow(10, FloorDecibels / 10);
        var starts = new List<int>();
        var strength = new List<double>();

        for (int at = 0; at < steps; at++)
        {
            if (rise[at] < RiseDecibels) continue;
            if (at + 1 < steps && rise[at + 1] > rise[at]) continue;
            if (Math.Max(all[at], at + 1 < steps ? all[at + 1] : 0) <= quiet) continue;

            if (starts.Count > 0 && at - starts[^1] < Math.Max(1, apart))
            {
                if (rise[at] > strength[^1])
                {
                    starts[^1] = at;
                    strength[^1] = rise[at];
                }

                continue;
            }

            starts.Add(at);
            strength.Add(rise[at]);
        }

        return starts;
    }

    /// <summary>Measures one hit and says what it is.</summary>
    private static DrumHit Judge(
        double[] whole, double[] low, double[] middle, double[] high, double[] all,
        int step, int rate, int from, int next, int count)
    {
        int begin = Math.Max(0, (from - 1) * step);
        int stop = Math.Min(count, next * step);

        int loudest = from;
        int attackSteps = Math.Max(1, (int)Math.Round(AttackSeconds * rate / step));

        for (int at = from; at < Math.Min(next, from + attackSteps + 1); at++)
            if (all[at] > all[loudest]) loudest = at;

        double peakEnergy = all[loudest];
        double decayLine = peakEnergy * Math.Pow(10, DecayDecibels / 10);
        double goneLine = peakEnergy * Math.Pow(10, GoneDecibels / 10);

        int decayed = -1, gone = -1;
        int longest = from + (int)Math.Round(LongestSeconds * rate / step);

        for (int at = loudest; at < all.Length && at < longest; at++)
        {
            if (decayed < 0 && all[at] < decayLine) decayed = at;
            if (all[at] < goneLine) { gone = at; break; }
        }

        if (decayed < 0) decayed = Math.Min(all.Length, longest);
        if (gone < 0) gone = Math.Min(all.Length, longest);

        int end = Math.Min(stop, Math.Min(count, (gone + 1) * step));

        double decaySeconds = Math.Max(0, (decayed - loudest) * step / (double)rate);

        int feature = Math.Min(stop, begin + (int)Math.Round(FeatureSeconds * rate));
        int before = Math.Max(0, begin - (int)Math.Round(BeforeSeconds * rate));

        double lowGrowth = Growth(low, before, begin, feature);
        double middleGrowth = Growth(middle, before, begin, feature);
        double highGrowth = Growth(high, before, begin, feature);
        double total = lowGrowth + middleGrowth + highGrowth;

        double lowShare = total > 0 ? lowGrowth / total : 0;
        double middleShare = total > 0 ? middleGrowth / total : 0;
        double highShare = total > 0 ? highGrowth / total : 0;

        double noise = Crossings(whole, begin, Math.Min(stop, begin + (int)Math.Round(NoiseSeconds * rate)), rate);

        double peak = 0;

        for (int i = begin; i < end; i++) peak = Math.Max(peak, Math.Abs(whole[i]));

        var sound = Sound(lowShare, middleShare, highShare, decaySeconds, noise);

        return new DrumHit(
            begin / (double)count,
            Math.Max(begin + 1, end) / (double)count,
            sound,
            lowShare,
            middleShare,
            highShare,
            decaySeconds,
            noise,
            Math.Min(1, peak),
            (stop - begin) / (double)rate);
    }

    /// <summary>
    /// How much a band grew from just before a hit to its first moments, in mean square.
    /// </summary>
    /// <remarks>
    /// Where nothing grew, which a band already ringing louder than the hit adds to it can do, what
    /// is in the band is used instead: something is still there, and the share should say so.
    /// </remarks>
    private static double Growth(double[] band, int before, int begin, int end)
    {
        double after = MeanSquare(band, begin, end);
        double was = MeanSquare(band, before, begin);
        double grew = after - was;

        return grew > 0 ? grew : after * 0.1;
    }

    /// <summary>The mean square of a stretch of a signal, or nought for no stretch.</summary>
    private static double MeanSquare(double[] signal, int from, int to)
    {
        if (to <= from) return 0;

        double sum = 0;

        for (int i = from; i < to; i++) sum += signal[i] * signal[i];

        return sum / (to - from);
    }

    /// <summary>How many times a stretch swings from one side of nought to the other, as a rate a second.</summary>
    /// <remarks>
    /// A swing only counts once it has gone past a small share of the stretch's loudest moment on
    /// the far side. Counting every change of sign counts the hiss of whatever was ringing quietly
    /// just before the hit, which crosses nought thousands of times a second at a level nobody
    /// hears, and a kick landing on a tail would be heard as a rattle.
    /// </remarks>
    private static double Crossings(double[] signal, int from, int to, int rate)
    {
        if (to - from < 2) return 0;

        double loudest = 0;

        for (int i = from; i < to; i++) loudest = Math.Max(loudest, Math.Abs(signal[i]));

        double band = loudest * SwingShare;

        if (band <= 0) return 0;

        int crossed = 0;
        int side = 0;

        for (int i = from; i < to; i++)
        {
            int now = signal[i] > band ? 1 : signal[i] < -band ? -1 : 0;

            if (now == 0) continue;

            if (side != 0 && now != side) crossed++;

            side = now;
        }

        return crossed * (double)rate / (to - from);
    }

    /// <summary>The rules: what a hit with these shares, this ring and this noise is.</summary>
    /// <param name="low">The bottom band's share.</param>
    /// <param name="middle">The middle band's share.</param>
    /// <param name="high">The top band's share.</param>
    /// <param name="decay">How long it takes to fall twenty decibels.</param>
    /// <param name="noise">Crossings of nought a second.</param>
    public static DrumSound Sound(double low, double middle, double high, double decay, double noise)
    {
        if (high >= MetalHigh || (noise >= MetalCrossings && high >= MetalShare))
            return decay < ClosedHatSeconds ? DrumSound.ClosedHat
                 : decay < OpenHatSeconds ? DrumSound.OpenHat
                 : DrumSound.Cymbal;

        if (low >= KickLow && noise < KickCrossings) return DrumSound.Kick;

        if (noise >= NoisyCrossings) return DrumSound.Snare;

        if (middle >= low && middle >= high) return decay < TomSeconds ? DrumSound.Snare : DrumSound.Tom;

        if (low >= middle && low >= high) return DrumSound.Kick;

        return DrumSound.Percussion;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DrumHit> Kit(IReadOnlyList<DrumHit>? hits, int pads)
    {
        if (hits is null || hits.Count == 0 || pads <= 0) return Array.Empty<DrumHit>();

        var groups = new List<List<DrumHit>>();

        double quietest = hits.Max(one => one.Peak) * QuietestShare;

        foreach (var hit in hits.Where(one => one.Peak >= quietest))
        {
            List<DrumHit>? nearest = null;
            double closest = SameDrum;

            foreach (var group in groups)
            {
                if (group[0].Sound != hit.Sound) continue;

                double distance = group.Average(one => Distance(one, hit));

                if (distance >= closest) continue;

                closest = distance;
                nearest = group;
            }

            if (nearest is null) groups.Add(new List<DrumHit> { hit });
            else nearest.Add(hit);
        }

        return groups
            .OrderBy(group => group[0].Sound)
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group[0].Start)
            .Take(pads)
            .Select(group => group
                .OrderByDescending(one => Math.Min(one.Clear, ClearEnough))
                .ThenByDescending(one => one.Peak)
                .First())
            .ToList();
    }

    /// <summary>How far apart two hits sound, where about one is a different drum.</summary>
    private static double Distance(DrumHit one, DrumHit other)
    {
        double low = one.Low - other.Low;
        double middle = one.Middle - other.Middle;
        double high = one.High - other.High;
        double ring = (Math.Log10(one.DecaySeconds + 0.01) - Math.Log10(other.DecaySeconds + 0.01)) * 0.5;
        double noise = (one.Noise - other.Noise) / 40000;

        return Math.Sqrt((low * low) + (middle * middle) + (high * high) + (ring * ring) + (noise * noise));
    }

    /// <inheritdoc/>
    public string NameOf(IReadOnlyList<DrumHit> kit, int at) =>
        kit is null || at < 0 || at >= kit.Count ? "" : Named(kit.Select(one => one.Sound).ToList(), at);

    /// <inheritdoc/>
    public IReadOnlyList<string> Names(IReadOnlyList<DrumHit>? hits, IReadOnlyList<(double Start, double End)>? windows)
    {
        if (windows is null || windows.Count == 0) return Array.Empty<string>();

        var sounds = windows.Select(window => Heard(hits, window.Start, window.End)).ToList();

        return Enumerable.Range(0, sounds.Count).Select(at => Named(sounds, at)).ToList();
    }

    /// <summary>
    /// How far before a piece a hit may begin and still be that piece's, as a share of the piece.
    /// </summary>
    /// <remarks>
    /// A hit is found a moment before its sound arrives, and a cut placed by hand or by a grid sits
    /// on the beat; so the hit that opens a piece often begins a few milliseconds before it, and
    /// the hit that opens the next piece a few milliseconds before the end of this one.
    /// </remarks>
    private const double EarlyShare = 0.1;

    /// <summary>What a window sounds like: the loudest hit beginning in it, or the one ringing into it.</summary>
    private static DrumSound Heard(IReadOnlyList<DrumHit>? hits, double start, double end)
    {
        if (hits is null || hits.Count == 0) return DrumSound.Percussion;

        double early = (end - start) * EarlyShare;

        var beginning = hits.Where(one => one.Start >= start - early && one.Start < end - early).ToList();

        if (beginning.Count > 0) return beginning.OrderByDescending(one => one.Peak).First().Sound;

        var ringing = hits.LastOrDefault(one => one.Start < start && one.End > start);

        return ringing?.Sound ?? DrumSound.Percussion;
    }

    /// <summary>The name of the sound at that place in a list, numbered where the list has more than one of it.</summary>
    private static string Named(IReadOnlyList<DrumSound> sounds, int at)
    {
        var sound = sounds[at];

        string name = sound switch
        {
            DrumSound.Kick => "Kick",
            DrumSound.Snare => "Snare",
            DrumSound.ClosedHat => "Hat closed",
            DrumSound.OpenHat => "Hat open",
            DrumSound.Tom => "Tom",
            DrumSound.Cymbal => "Cymbal",
            _ => "Perc",
        };

        int same = sounds.Count(one => one == sound);

        if (same < 2) return name;

        int number = sounds.Take(at + 1).Count(one => one == sound);

        return name + " " + number;
    }
}
