using System;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Tracker;

/// <summary>
/// Where a recording gets cut into pieces. Nothing here touches the file: a cut is a position
/// in the sound, and the sound stays whole.
/// </summary>
/// <remarks>
/// Slice points are fractions of the recording, and there is always one more of them than there
/// are slices, because the two ends are points as well. That is what makes them editable: the
/// first point is where the sliced region begins, the last is where it ends, and every point in
/// between is a boundary two slices share. Move one and two slices change. Take one away and
/// the two either side become one.
///
/// Which is also why the head is a point rather than a pinned zero. A take almost always opens
/// with a moment of nothing before the first hit, and that silence has to belong to no slice
/// rather than to the first one.
/// </remarks>
public static class SampleSlicer
{
    /// <summary>
    /// The most pieces this will ever cut a recording into. Whoever takes the pieces holds
    /// fewer than this and clamps again: a kit has sixteen pads, a map thirty-two zones.
    /// </summary>
    public const int MaxSlices = 32;

    /// <summary>
    /// The shortest a slice is allowed to be. A drum hit is several rising moments, not one,
    /// and without a floor under the spacing the loudest of them are all found separately.
    /// </summary>
    public const double MinSliceSeconds = 0.03;

    /// <summary>A last step back once the attack has been found. Early costs silence, late costs the hit.</summary>
    private const double BackOffSeconds = 0.006;

    /// <summary>
    /// How far back from a loud moment the sound that made it is looked for. Comfortably less
    /// than the shortest slice, so walking back can never reach the hit before.
    /// </summary>
    private const double AttackSeconds = 0.02;

    /// <summary>
    /// Below this share of the moment's own loudness, the sound had not started yet. A struck
    /// sound is loudest a few milliseconds in rather than on its first sample, so the loudest
    /// moment is where the search begins and never where the cut lands.
    /// </summary>
    private const double AttackFloor = 0.15;

    /// <summary>How far above what is still ringing a moment has to be to read as an attack.</summary>
    private const double RiseFactor = 1.8;

    /// <summary>
    /// How long the loudest moment so far is remembered for, falling away as it goes.
    /// </summary>
    /// <remarks>
    /// The whole of the difference between finding hits and finding loud noises. A struck sound
    /// decays, and noise in its tail is easily louder than the average of the recording just
    /// gone, so a mean says "another hit" halfway through the first one. What has to be beaten
    /// is not the average but what is still ringing, and what is still ringing is the last peak
    /// falling away. Short enough that two hits of the same level a slice apart are both found,
    /// long enough that nothing inside one hit is ever taken for a second.
    /// </remarks>
    private const double HoldSeconds = 0.04;

    /// <summary>Below this there is nothing there, whatever else is true.</summary>
    private const float NoiseFloor = 0.02f;

    /// <summary>
    /// Quieter than this share of the recording's loudest moment and nothing is being said.
    /// Measured against the recording rather than fixed, because one take's silence is another
    /// take's quiet passage.
    /// </summary>
    private const double QuietShare = 0.06;

    /// <summary>How much of what follows a gap is listened to, to judge whether it is one.</summary>
    private const double AfterSeconds = 0.15;

    /// <summary>
    /// The least quiet that counts as a gap at all. Deliberately short: which gaps are real is
    /// settled by taking the longest, not by refusing the short ones up front.
    /// </summary>
    /// <remarks>
    /// A spoken take does not space its words evenly. The first few numbers of a countdown are
    /// separated by a fifth of a second and the last few by a twentieth, because the voice runs
    /// them together as it goes. A fixed minimum long enough for the early gaps throws the late
    /// ones away and the last words come out as one piece.
    /// </remarks>
    private const double MinGapSeconds = 0.03;

    /// <summary>Evenly spaced points, for a loop with nothing in it to find.</summary>
    public static List<double> Even(int slices)
    {
        slices = Math.Clamp(slices, 1, MaxSlices);

        var points = new List<double>(slices + 1);

        for (int i = 0; i <= slices; i++) points.Add(i / (double)slices);

        return points;
    }

    /// <summary>
    /// Points at the attacks, up to the number asked for, falling back to an even division when
    /// the recording has too few to work with.
    /// </summary>
    /// <param name="peaks">Loudest moment per bucket across the whole recording, 0 to 1.</param>
    /// <param name="lengthSeconds">How long the recording is, for the spacing rules.</param>
    /// <param name="slices">How many pieces to aim for. Fewer come back when fewer are there.</param>
    public static List<double> Transients(IReadOnlyList<float>? peaks, double lengthSeconds, int slices)
    {
        slices = Math.Clamp(slices, 1, MaxSlices);

        if (peaks == null || peaks.Count < 8 || lengthSeconds <= 0) return Even(slices);

        double perBucket = lengthSeconds / peaks.Count;
        int apart = Apart(lengthSeconds, slices, perBucket);
        int backOff = Math.Max(1, (int)Math.Round(BackOffSeconds / perBucket));

        int reach = Math.Max(1, (int)Math.Round(AttackSeconds / perBucket));

        // Where each loud moment's sound began, rather than where it was loudest. Done before
        // the spacing rule, so two loud moments inside one hit fall back onto the same start
        // and the second is then dropped for being on top of the first.
        var rises = Rises(peaks, perBucket)
            .Select(rise => (Bucket: Began(peaks, rise.Bucket, reach), rise.Strength))
            .ToList();

        var taken = Spaced(rises, slices, apart);

        // One attack is not a slicing, it is a recording. Even division says more than a single
        // cut in an arbitrary place does.
        if (taken.Count < 2) return Even(slices);

        var points = new List<double>(taken.Count + 1);

        foreach (int bucket in taken)
            points.Add(Math.Max(0, bucket - backOff) / (double)peaks.Count);

        points.Add(1.0);

        return points;
    }

    /// <summary>
    /// Points at the silences, for a recording that is things separated by nothing rather than
    /// things struck.
    /// </summary>
    /// <remarks>
    /// What a spoken take needs, and what looking for attacks cannot give it. A word is several
    /// attacks, one to a syllable, and the quietest moment inside a word is louder than the pause
    /// after it, so a detector that ranks by loudness cuts up the words and runs the pauses
    /// together. Ten spoken numbers have nine silences in them and no reliable attacks at all.
    ///
    /// The cut goes at the end of each silence rather than the start, because what is being found
    /// is where the next thing begins.
    /// </remarks>
    public static List<double> Gaps(IReadOnlyList<float>? peaks, double lengthSeconds, int slices)
    {
        slices = Math.Clamp(slices, 1, MaxSlices);

        if (peaks == null || peaks.Count < 8 || lengthSeconds <= 0) return Even(slices);

        double loudest = 0;
        foreach (float peak in peaks) loudest = Math.Max(loudest, peak);

        if (loudest <= NoiseFloor) return Even(slices);

        double quiet = Math.Max(NoiseFloor, loudest * QuietShare);
        double perBucket = lengthSeconds / peaks.Count;
        int shortest = Math.Max(1, (int)Math.Round(MinGapSeconds / perBucket));
        int backOff = Math.Max(1, (int)Math.Round(BackOffSeconds / perBucket));

        var silences = Silences(peaks, quiet, shortest);

        // A silence at the very front is the lead-in, and one at the very back is what is left
        // after the last thing: neither divides one piece from another, but both say where the
        // sliced region begins and ends.
        int head = silences.Count > 0 && silences[0].From == 0 ? silences[0].To : 0;
        int tail = silences.Count > 0 && silences[^1].To >= peaks.Count ? silences[^1].From : peaks.Count;

        int listen = Math.Max(1, (int)Math.Round(AfterSeconds / perBucket));

        // Scored by what starts after the gap, not by how long the gap is. A word decaying away
        // can be quiet for longer than the pause before the next word, so length picks the
        // middle of a word over the space between two. What marks a gap as real is that
        // something loud begins on the other side of it.
        var candidates = silences
            .Where(gap => gap.From > head && gap.To < tail)
            .Select(gap => (
                Bucket: Math.Max(gap.From, gap.To - backOff),
                Strength: Loudest(peaks, gap.To, listen)))
            .ToList();

        // And then kept apart, for the same reason the attacks are: two silences a moment apart
        // both hear the same word start after them, and both would be taken.
        var between = Spaced(candidates, slices - 1, Apart(lengthSeconds, slices, perBucket));

        if (between.Count == 0) return Even(slices);

        var points = new List<double>(between.Count + 2)
        {
            Math.Max(0, head - backOff) / (double)peaks.Count
        };

        foreach (int at in between) points.Add(at / (double)peaks.Count);

        points.Add(Math.Min(peaks.Count, tail + backOff) / (double)peaks.Count);

        return points;
    }

    /// <summary>
    /// How far apart two cuts have to be, in buckets.
    /// </summary>
    /// <remarks>
    /// Asking for ten pieces of a fourteen second recording is saying they are about a second
    /// and a half each, so a piece a twentieth of that long is not one of the ten. The floor
    /// underneath keeps a fast drum roll cuttable, where the pieces really are that short.
    /// </remarks>
    private static int Apart(double lengthSeconds, int slices, double perBucket)
    {
        double wanted = Math.Max(MinSliceSeconds, lengthSeconds / slices / 4);

        return Math.Max(1, (int)Math.Round(wanted / perBucket));
    }

    /// <summary>The loudest moment in the stretch just after a point: what begins there.</summary>
    private static double Loudest(IReadOnlyList<float> peaks, int from, int length)
    {
        double loudest = 0;

        for (int i = from; i < from + length && i < peaks.Count; i++)
            loudest = Math.Max(loudest, peaks[i]);

        return loudest;
    }

    /// <summary>Every stretch quiet enough, and long enough, to be a gap rather than a dip.</summary>
    private static List<(int From, int To)> Silences(IReadOnlyList<float> peaks, double quiet, int shortest)
    {
        var silences = new List<(int, int)>();
        int from = -1;

        for (int i = 0; i < peaks.Count; i++)
        {
            if (peaks[i] <= quiet)
            {
                if (from < 0) from = i;
                continue;
            }

            if (from >= 0 && i - from >= shortest) silences.Add((from, i));

            from = -1;
        }

        if (from >= 0 && peaks.Count - from >= shortest) silences.Add((from, peaks.Count));

        return silences;
    }

    /// <summary>
    /// Walks back from a loud moment to where the sound that made it started: the last point
    /// still quiet compared to it. Never further than <see cref="AttackSeconds"/>, so a hit
    /// with a long tail behind it cannot swallow the one before.
    /// </summary>
    private static int Began(IReadOnlyList<float> peaks, int bucket, int reach)
    {
        double quiet = peaks[bucket] * AttackFloor;

        int at = bucket;
        int stop = Math.Max(0, bucket - reach);

        while (at > stop && peaks[at - 1] > quiet) at--;

        return at;
    }

    /// <summary>
    /// Every moment louder than the recording had just been, with how much louder it was. The
    /// same attack shows up as several of these; keeping them apart is the next step's job.
    /// </summary>
    private static List<(int Bucket, double Strength)> Rises(IReadOnlyList<float> peaks, double perBucket)
    {
        var rises = new List<(int, double)>();

        double fade = Math.Exp(-perBucket / HoldSeconds);
        double ringing = 0;

        for (int i = 0; i < peaks.Count; i++)
        {
            float peak = peaks[i];

            if (peak > NoiseFloor && peak > ringing * RiseFactor) rises.Add((i, peak - ringing));

            // Asked before it is updated, or every attack would be measured against itself.
            ringing = Math.Max(peak, ringing * fade);
        }

        return rises;
    }

    /// <summary>
    /// The loudest attacks that are not on top of each other, in the order they happen.
    /// </summary>
    /// <remarks>
    /// Strongest first rather than earliest first on purpose. A take of eight hits has hundreds
    /// of moments that rise; taking the earliest that fit would fill the count with whatever
    /// happened to be at the front of the recording and stop before the real hits later on.
    /// </remarks>
    private static List<int> Spaced(List<(int Bucket, double Strength)> rises, int want, int apart)
    {
        var taken = new List<int>(want);

        foreach (var rise in rises.OrderByDescending(r => r.Strength))
        {
            if (taken.Count >= want) break;

            bool crowded = taken.Any(t => Math.Abs(t - rise.Bucket) < apart);

            if (!crowded) taken.Add(rise.Bucket);
        }

        taken.Sort();

        return taken;
    }

    /// <summary>
    /// Puts a hand-edited list back in order: inside the recording, rising, and never so close
    /// together that a slice has nothing in it.
    /// </summary>
    public static List<double> Clean(IEnumerable<double> points, double lengthSeconds)
    {
        double apart = lengthSeconds > 0
            ? Math.Min(MinSliceSeconds / lengthSeconds, 0.5)
            : 0;

        var sorted = points
            .Where(p => !double.IsNaN(p))
            .Select(p => Math.Clamp(p, 0, 1))
            .OrderBy(p => p)
            .ToList();

        var cleaned = new List<double>(sorted.Count);

        foreach (double point in sorted)
        {
            if (cleaned.Count > 0 && point - cleaned[^1] < apart) continue;

            cleaned.Add(point);
        }

        // Everything landed in one place: there is no slicing left to describe.
        if (cleaned.Count < 2) return new List<double> { 0, 1 };

        if (cleaned.Count > MaxSlices + 1) cleaned.RemoveRange(MaxSlices + 1, cleaned.Count - MaxSlices - 1);

        return cleaned;
    }
}
