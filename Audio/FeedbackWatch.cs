using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class FeedbackWatch : IFeedbackWatch
{
    /// <summary>How many samples one look at the spectrum is taken over.</summary>
    /// <remarks>
    /// 512 is about 12 ms at 44.1k and puts a bin every 86 Hz, which is coarse for naming a note
    /// and ample for saying that one bin towers over its neighbours. **Time resolution is what
    /// matters here and frequency resolution is not**: nothing downstream cares which frequency is
    /// ringing, only that one of them is, and a longer frame would buy precision nobody reads at
    /// the price of being slower to say it.
    /// </remarks>
    private const int Frame = 512;

    /// <summary>How far the window moves between looks.</summary>
    /// <remarks>Half a frame, so a run is confirmed on overlapping evidence roughly every 6 ms.</remarks>
    private const int Hop = 256;

    /// <summary>How many looks in a row have to agree before it is said.</summary>
    /// <remarks>
    /// Four, which is about 23 ms at 44.1k. **Feedback is fast and that is the whole reason this
    /// is not a level watcher**: the difference between saying it in twenty milliseconds and
    /// saying it in three hundred is the difference between a chirp and a scream in the room.
    /// Fewer than four and a transient with a tonal moment in it would be enough.
    /// </remarks>
    private const int Sure = 4;

    /// <summary>Below this nothing is ringing, whatever shape it is.</summary>
    /// <remarks>
    /// About -50 dBFS. A quiet room through an open microphone is noise with a crest of its own
    /// and there is no sense reading its spectrum, and a ring that matters is never this small by
    /// the time four frames have passed.
    /// </remarks>
    private const float Quiet = 0.003f;

    /// <summary>The most peak-over-average a signal may have and still be called tonal.</summary>
    /// <remarks>
    /// A sine's is the square root of two, about 1.41. Speech runs three to five and music higher.
    /// 2.2 leaves room for a ring that is not yet pure without letting a voice through.
    /// </remarks>
    private const float Crest = 2.2f;

    /// <summary>How far a bin must stand over the ones beside it.</summary>
    /// <remarks>
    /// **Read three bins out rather than one, which is not a tuning but the window's own shape.**
    /// A raised cosine spreads a single frequency across about four bins, so the bin next door is
    /// part of the same peak: measured there a pure tone reads 1.17, which is no peak at all, and
    /// the test can never pass however loud the room gets. Three out is clear of the lobe, where
    /// the same tone reads 46.
    ///
    /// Measured rather than reasoned about, because the first version had it at one bin and
    /// rejected every ring there is while passing everything it was supposed to reject. **A guard
    /// that can only ever answer no is the same fault as one that can only ever answer yes**, and
    /// the tell was that four tests agreed and none of them was the one that mattered.
    /// </remarks>
    private const float OverNeighbours = 8f;

    /// <summary>How far out the bins beside it are read.</summary>
    /// <inheritdoc cref="OverNeighbours"/>
    private const int Beside = 3;

    /// <summary>And over the whole spectrum's own average.</summary>
    private const float OverMean = 30f;

    /// <summary>
    /// The most a harmonic may hold, as a share of the bin under test, for it to be a ring.
    /// </summary>
    /// <remarks>
    /// **What tells a ring from a played note of the same pitch.** An instrument or a voice brings
    /// a series with it; a ring is one frequency, because the loop has most gain at one frequency.
    /// Read at twice the bin, which is the octave and the strongest of any series.
    /// </remarks>
    private const float HarmonicShare = 0.25f;

    /// <summary>
    /// How far the ring must stand over the next loudest thing in the spectrum.
    /// </summary>
    /// <remarks>
    /// **The strongest test of the lot, and it was missing.** A ring is one frequency, because the
    /// loop has most gain at one frequency; music is never one frequency. A mix, a chord, a bass
    /// note with a room around it and a voice all put a second peak somewhere near the first, and
    /// this is what reads it.
    ///
    /// It was left out because the crest was expected to do that work, and the crest cannot: over
    /// a frame of eleven milliseconds a sustained note is as tonal as a sine, and mastered music
    /// is limited flat besides. **Crest separates tonal from percussive over a long window and
    /// says almost nothing over a short one**, which is the window this has to use to be quick.
    ///
    /// Reported as capturing a second card's playback and having listening shut off at once,
    /// which is exactly what one loud note in a mix would do.
    /// </remarks>
    private const float OverSecond = 6f;

    /// <summary>The lowest a ring is looked for at.</summary>
    /// <remarks>
    /// **Two reasons, and the second is the one that was got wrong.** A loop through the speakers
    /// and microphone a computer has built into it rings somewhere between one and six kilohertz,
    /// because neither end moves enough air to sustain anything low; there is nothing under three
    /// hundred to find.
    ///
    /// And the bins down there are where a bass note lives, which at eighty six hertz a bin is one
    /// or two bins wide and impossible to read. Left in the search, a fundamental too low to
    /// resolve went missing while its second partial stood alone in a clear spectrum and read
    /// exactly like a ring. **A bound that throws away the evidence for the thing it is judging
    /// makes the judgement worse rather than cheaper**, which is what a bass note swelling proved
    /// the moment it was written down as a test.
    ///
    /// Only the candidate is bounded. Everything from the bottom still counts as company, which is
    /// what that bass note needed: the fundamental it could not resolve is still enough to say the
    /// spectrum is not empty.
    ///
    /// **Eight hundred and not three, and the three was measured being wrong.** At three hundred
    /// this landed on bin 3 of a 48 kHz stream, which is 281 Hz, and the next-peak test skips the
    /// bins within <see cref="Beside"/> of the candidate — so bins 0 to 6, everything under 656
    /// Hz, was hidden from the one test that is supposed to notice music. A low swell then read as
    /// a lone tone 58 times over a next peak that had been excluded from being itself:
    /// <c>ringing at bin 3, 67 over the mean, 33 over the bins beside it, 58 over the next peak,
    /// crest 1.66, grown 1.50</c>, off a card with no microphone anywhere near it.
    ///
    /// **A floor and an exclusion window are the same rule twice and have to be read together.**
    /// Put the candidate on the lowest bin it is allowed and the window reaches below the bottom
    /// of the spectrum, so the company test is asking about a band that has been cleared of
    /// company. Eight hundred puts the window clear of the bass at either rate, which is the
    /// number's real justification; that a computer's own speaker cannot sustain a loop down there
    /// is the reason it costs nothing.
    ///
    /// Rounded up rather than truncated, for the same reason: it is a floor.
    /// </remarks>
    private const double LowestHertz = 800;

    /// <summary>How much the ring must have grown across the run.</summary>
    /// <remarks>
    /// About three decibels over the four frames. **This is the test that keeps a test tone out**:
    /// a sine played into a microphone on purpose is narrow, tonal and persistent, and the one
    /// thing it does not do is climb.
    /// </remarks>
    private const float Growth = 1.4f;

    /// <summary>Samples waiting to be looked at, summed to one channel.</summary>
    private float[] _fifo = new float[Frame * 4];

    /// <summary>How many of them there are.</summary>
    private int _held;

    /// <summary>The real and imaginary halves of the transform, kept so nothing allocates per frame.</summary>
    private readonly float[] _real = new float[Frame];

    /// <inheritdoc cref="_real"/>
    private readonly float[] _imaginary = new float[Frame];

    /// <summary>The raised cosine the frame is read through, worked out once.</summary>
    /// <remarks>
    /// Without a window a frame's own edges spread across every bin, which is exactly the
    /// measurement being made here: how far one bin stands over the rest.
    /// </remarks>
    private readonly float[] _window = Window();

    /// <summary>How many looks in a row have agreed.</summary>
    private int _agreed;

    /// <summary>Which bin they agreed about.</summary>
    private int _bin;

    /// <summary>And how big it was when the run began.</summary>
    private float _began;

    /// <summary>The lowest bin a ring may be found in, worked out from the rate.</summary>
    private int _lowest = 2;

    /// <summary>Whether it has already said so and is waiting to be armed again.</summary>
    /// <remarks>
    /// **Latched rather than merely forgotten**, and the difference is the whole of what a caller
    /// wants. A ring that has been reported goes on ringing for as long as it takes somebody to do
    /// something about it, so a run that simply started over would say it again every twenty
    /// milliseconds all the way up. Latched, it is one answer to one event, and what ends it is
    /// the deliberate act of listening again.
    /// </remarks>
    private bool _rang;

    /// <inheritdoc/>
    public void Clear()
    {
        _held = 0;
        _agreed = 0;
        _bin = -1;
        _began = 0;
        _rang = false;
    }

    /// <inheritdoc/>
    public bool Ringing(float[] block, int floats, int rate)
    {
        if (block is null || floats <= 1) return false;
        if (_rang) return false;

        _lowest = Math.Max(2, (int)Math.Ceiling(LowestHertz * Frame / Math.Max(1, rate)));

        Take(block, Math.Min(floats, block.Length));

        bool rang = false;

        while (_held >= Frame)
        {
            if (Look()) rang = true;

            Array.Copy(_fifo, Hop, _fifo, 0, _held - Hop);
            _held -= Hop;

            if (rang) break;
        }

        if (rang)
        {
            _held = 0;
            _agreed = 0;
            _bin = -1;
            _began = 0;
            _rang = true;
        }

        return rang;
    }

    /// <summary>Sums the block to one channel and keeps it.</summary>
    /// <remarks>
    /// One channel because a ring is in both: the loop is the room rather than a side, and reading
    /// two spectra to compare them with each other would be twice the work for an answer neither
    /// half disagrees about.
    ///
    /// A block longer than there is room for is dropped at the front, which is the right end to
    /// lose: what matters is the most recent frames, and a caller handing over a second of audio
    /// at a time has already lost the timing this is about.
    /// </remarks>
    /// <param name="block">Interleaved stereo samples.</param>
    /// <param name="floats">How many are really there.</param>
    private void Take(float[] block, int floats)
    {
        int frames = floats / 2;

        if (frames <= 0) return;

        if (_fifo.Length < frames + Frame) _fifo = new float[frames + Frame];

        if (_held + frames > _fifo.Length)
        {
            int keep = Math.Max(0, _fifo.Length - frames);

            if (keep > 0 && _held > keep) Array.Copy(_fifo, _held - keep, _fifo, 0, keep);

            _held = Math.Min(_held, keep);
        }

        for (int at = 0; at < frames; at++)
            _fifo[_held + at] = (block[at * 2] + block[(at * 2) + 1]) * 0.5f;

        _held += frames;
    }

    /// <summary>One look at the frame at the front, and whether the run is now long enough.</summary>
    /// <remarks>
    /// Every test has to pass for the run to go on, and a frame that fails any of them ends it.
    /// That is deliberate rather than a score: what is wanted is four frames that all look the
    /// same way, and a running total would let two convincing frames carry two that were not.
    /// </remarks>
    /// <returns>True where this frame completes a run.</returns>
    private bool Look()
    {
        float peak = 0;
        double square = 0;

        for (int at = 0; at < Frame; at++)
        {
            float one = _fifo[at];
            float size = Math.Abs(one);

            if (size > peak) peak = size;

            square += (double)one * one;

            _real[at] = one * _window[at];
            _imaginary[at] = 0;
        }

        float rms = (float)Math.Sqrt(square / Frame);

        if (peak < Quiet || rms <= 0) return Broke();
        if (peak / rms > Crest) return Broke();

        Transform();

        int bins = Frame / 2;
        int loudest = -1;
        float most = 0;
        double total = 0;

        for (int at = 1; at < bins; at++)
        {
            float size = Size(at);

            total += size;

            if (at < _lowest || size <= most) continue;

            most = size;
            loudest = at;
        }

        if (loudest < 0 || most <= 0) return Broke();

        float mean = (float)(total / (bins - 1));

        if (mean <= 0 || most / mean < OverMean) return Broke();

        float under = loudest - Beside >= 1 ? Size(loudest - Beside) : 0;
        float over = loudest + Beside < bins ? Size(loudest + Beside) : 0;
        float beside = Math.Max(under, over);

        if (beside > 0 && most / beside < OverNeighbours) return Broke();

        int octave = loudest * 2;

        if (octave < bins && Size(octave) > most * HarmonicShare) return Broke();

        float second = 0;

        for (int at = 1; at < bins; at++)
        {
            if (Math.Abs(at - loudest) <= Beside) continue;

            float size = Size(at);

            if (size > second) second = size;
        }

        if (second > 0 && most / second < OverSecond) return Broke();

        if (_agreed > 0 && Math.Abs(loudest - _bin) <= 1)
        {
            _agreed++;
        }
        else
        {
            _agreed = 1;
            _began = most;
        }

        _bin = loudest;

        bool sure = _agreed >= Sure && most >= _began * Growth;

        if (sure)
            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, () =>
                "monitor: ringing at bin " + loudest + ", " + (most / mean).ToString("F0")
                + " over the mean, " + (beside > 0 ? (most / beside).ToString("F0") : "clear")
                + " over the bins beside it, " + (second > 0 ? (most / second).ToString("F0") : "clear")
                + " over the next peak, crest " + (peak / rms).ToString("F2")
                + ", grown " + (most / _began).ToString("F2"));

        return sure;
    }

    /// <summary>Ends the run and says so.</summary>
    private bool Broke()
    {
        _agreed = 0;
        _bin = -1;
        _began = 0;

        return false;
    }

    /// <summary>How big one bin is.</summary>
    private float Size(int bin) =>
        MathF.Sqrt((_real[bin] * _real[bin]) + (_imaginary[bin] * _imaginary[bin]));

    /// <summary>
    /// The transform, in place, over <see cref="_real"/> and <see cref="_imaginary"/>.
    /// </summary>
    /// <remarks>
    /// **Its own rather than a seam of its own**, which is the one place here that does not follow
    /// this codebase's usual rule, and the reason is what it is: this is not a transform anybody
    /// else asks for, it is how this watch reaches its answer, and the thing worth being able to
    /// stand in front of is the answer. A general one would be a different exercise with its own
    /// contract, its own rates and its own tests, and nothing would use it.
    ///
    /// Iterative radix two, which is all <see cref="Frame"/> being a power of two allows and all
    /// that is wanted: 512 points is a few microseconds and runs on the thread the capture arrives
    /// on, beside the clipping test that is already there.
    /// </remarks>
    private void Transform()
    {
        for (int at = 1, place = 0; at < Frame; at++)
        {
            int bit = Frame >> 1;

            for (; (place & bit) != 0; bit >>= 1) place ^= bit;

            place ^= bit;

            if (at >= place) continue;

            (_real[at], _real[place]) = (_real[place], _real[at]);
            (_imaginary[at], _imaginary[place]) = (_imaginary[place], _imaginary[at]);
        }

        for (int span = 2; span <= Frame; span <<= 1)
        {
            double step = -2.0 * Math.PI / span;

            for (int start = 0; start < Frame; start += span)
            {
                for (int at = 0; at < span / 2; at++)
                {
                    double angle = step * at;
                    float cos = (float)Math.Cos(angle);
                    float sin = (float)Math.Sin(angle);

                    int here = start + at;
                    int there = here + (span / 2);

                    float realOther = (_real[there] * cos) - (_imaginary[there] * sin);
                    float otherImaginary = (_real[there] * sin) + (_imaginary[there] * cos);

                    _real[there] = _real[here] - realOther;
                    _imaginary[there] = _imaginary[here] - otherImaginary;
                    _real[here] += realOther;
                    _imaginary[here] += otherImaginary;
                }
            }
        }
    }

    /// <summary>The raised cosine every frame is read through.</summary>
    private static float[] Window()
    {
        var made = new float[Frame];

        for (int at = 0; at < Frame; at++)
            made[at] = 0.5f - (0.5f * MathF.Cos(2f * MathF.PI * at / (Frame - 1)));

        return made;
    }
}
