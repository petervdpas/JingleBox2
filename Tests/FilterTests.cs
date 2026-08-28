using System;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The four things in a voice or a strip that hold state between samples: the fixed filter, the
/// sweeping one, the pair of them cascaded, and the side chain.
/// </summary>
/// <remarks>
/// A filter is the one place in an audio path where a single bad number is not a single bad
/// sample. The state is fed back into itself, so a NaN that gets in stays in until somebody
/// clears it, and a filter that goes unstable at the top of its resonance takes the mix with it.
/// Most of what is here is therefore about the ends of the ranges and about what a filter does
/// after several thousand samples rather than after one.
///
/// No hardware and no window: every one of these takes a sample rate and a number.
///
/// Several tests below record what the code does today rather than what it should do; each one
/// says so in its own documentation.
/// </remarks>
public class FilterTests
{
    /// <summary>The rate everything here is worked out against, unless a test says otherwise.</summary>
    private const int Rate = 44100;

    /// <summary>Long enough for any of these to have settled on whatever it is going to do.</summary>
    private const int Settled = 4000;

    /// <summary>
    /// A full scale sine at a given frequency, which is the worst thing to hand a resonant
    /// filter when the frequency is its own cutoff.
    /// </summary>
    /// <param name="hz">The tone's frequency.</param>
    /// <param name="frames">How many samples of it.</param>
    private static double[] Tone(double hz, int frames)
    {
        var samples = new double[frames];

        for (int i = 0; i < frames; i++)
            samples[i] = Math.Sin(2 * Math.PI * hz * i / Rate);

        return samples;
    }

    /// <summary>Runs a block through a fixed filter and keeps what came out.</summary>
    /// <param name="filter">The filter under test.</param>
    /// <param name="input">What to feed it.</param>
    private static double[] Run(IToneFilter filter, double[] input)
    {
        var output = new double[input.Length];

        for (int i = 0; i < input.Length; i++)
            output[i] = filter.Process(input[i]);

        return output;
    }

    /// <summary>Runs a block through a sweeping filter, out of whichever end is asked for.</summary>
    /// <param name="filter">The filter under test.</param>
    /// <param name="input">What to feed it.</param>
    /// <param name="mode">Which end to take.</param>
    private static double[] Run(ISweepFilter filter, double[] input, FilterMode mode)
    {
        var output = new double[input.Length];

        for (int i = 0; i < input.Length; i++)
            output[i] = filter.Process(input[i], mode);

        return output;
    }

    /// <summary>Runs a block through both stages of a ladder.</summary>
    /// <param name="filter">The filter under test.</param>
    /// <param name="input">What to feed it.</param>
    private static double[] Run(ILadderFilter filter, double[] input)
    {
        var output = new double[input.Length];

        for (int i = 0; i < input.Length; i++)
            output[i] = filter.Process(input[i]);

        return output;
    }

    /// <summary>The loudest thing in a block, ignoring which way up it was.</summary>
    /// <param name="samples">The block to look through.</param>
    /// <param name="from">Where to start, so a settled tail can be read without its transient.</param>
    private static double Peak(double[] samples, int from = 0)
    {
        double peak = 0;

        for (int i = from; i < samples.Length; i++)
            peak = Math.Max(peak, Math.Abs(samples[i]));

        return peak;
    }

    /// <summary>A block of the same value, over and over.</summary>
    /// <param name="value">The value to repeat.</param>
    /// <param name="frames">How many of them.</param>
    private static double[] Steady(double value, int frames)
    {
        var samples = new double[frames];
        Array.Fill(samples, value);

        return samples;
    }

    /// <summary>The ends of the range are on the contract rather than only on the class.</summary>
    /// <remarks>
    /// They are written twice, as a constant anything can reach at compile time and as an
    /// explicit implementation of the interface. Two spellings of one number is exactly the
    /// arrangement that comes apart quietly, so this reads both.
    /// </remarks>
    [Fact]
    public void The_ends_of_the_range_are_named_on_the_contract()
    {
        IToneFilter filter = new ToneFilter(1000, 0, Rate);

        Assert.Equal(ToneFilter.OpenHz, filter.OpenHz, 12);
        Assert.Equal(ToneFilter.MinHz, filter.MinHz, 12);
        Assert.Equal(ToneFilter.MinResonance, filter.MinResonance, 12);
        Assert.Equal(ToneFilter.MaxResonance, filter.MaxResonance, 12);
        Assert.Equal(ToneFilter.NyquistMargin, filter.NyquistMargin, 12);
    }

    /// <summary>
    /// A filter at the top of its range is not a filter, and hands back exactly what it was
    /// given, including the values nothing else here survives.
    /// </summary>
    [Fact]
    public void Wide_open_passes_everything_straight_through()
    {
        IToneFilter filter = new ToneFilter(ToneFilter.OpenHz, 0.5, Rate);

        Assert.True(filter.IsOpen);
        Assert.Equal(0.25, filter.Process(0.25), 12);
        Assert.Equal(-1.0, filter.Process(-1.0), 12);
        Assert.True(double.IsNaN(filter.Process(double.NaN)));
        Assert.True(double.IsPositiveInfinity(filter.Process(double.PositiveInfinity)));
        Assert.Equal(0.25, filter.Process(0.25), 12);
    }

    /// <summary>A cutoff that is not a number at all reads as wide open.</summary>
    /// <remarks>
    /// A patch is JSON on disc, so this is the reading a file somebody has edited produces. The
    /// alternative is a voice that is silent for its whole life with nothing to say why.
    /// </remarks>
    [Fact]
    public void A_cutoff_that_is_not_a_number_reads_as_wide_open()
    {
        IToneFilter filter = new ToneFilter(double.NaN, 0, Rate);

        Assert.True(filter.IsOpen);
        Assert.Equal(0.6, filter.Process(0.6), 12);
    }

    /// <summary>
    /// A cutoff up near half the sample rate has nothing left to filter, so it stands down.
    /// </summary>
    /// <remarks>
    /// At the rates a card actually runs at nothing can reach the margin, because the control
    /// stops at twenty kilohertz first. At eight thousand it is reachable, and the margin is
    /// what keeps the tangent away from the point where it runs off to infinity.
    /// </remarks>
    [Fact]
    public void A_cutoff_near_half_the_rate_is_no_filter_at_all()
    {
        Assert.True(new ToneFilter(5000, 0, 8000).IsOpen);
        Assert.True(new ToneFilter(3970, 0, 8000).IsOpen);
        Assert.False(new ToneFilter(2000, 0, 8000).IsOpen);

        IToneFilter absurd = new ToneFilter(1000, 0, 1);
        Assert.True(absurd.IsOpen);
        Assert.Equal(0.4, absurd.Process(0.4), 12);
    }

    /// <summary>A cutoff of nought or below lands on the bottom of the range rather than at nought.</summary>
    /// <remarks>
    /// Twenty hertz is as closed as the control goes, and a cutoff of nought would be a filter
    /// with no bandwidth at all: a tangent of nought, coefficients of nought, and silence.
    /// </remarks>
    [Fact]
    public void A_cutoff_of_nought_or_below_lands_on_the_bottom_of_the_range()
    {
        var input = Tone(200, 400);

        double[] atNought = Run(new ToneFilter(0, 0.3, Rate), input);
        double[] belowNought = Run(new ToneFilter(-5000, 0.3, Rate), input);
        double[] atTheBottom = Run(new ToneFilter(ToneFilter.MinHz, 0.3, Rate), input);

        Assert.False(new ToneFilter(0, 0.3, Rate).IsOpen);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(atTheBottom[i], atNought[i], 12);
            Assert.Equal(atTheBottom[i], belowNought[i], 12);
        }
    }

    /// <summary>Resonance past either end is held there rather than carried into the coefficients.</summary>
    [Fact]
    public void Resonance_past_either_end_is_held()
    {
        var input = Tone(1000, 400);

        double[] tooMuch = Run(new ToneFilter(1000, 5, Rate), input);
        double[] atTheTop = Run(new ToneFilter(1000, ToneFilter.MaxResonance, Rate), input);
        double[] tooLittle = Run(new ToneFilter(1000, -5, Rate), input);
        double[] atTheBottom = Run(new ToneFilter(1000, ToneFilter.MinResonance, Rate), input);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(atTheTop[i], tooMuch[i], 12);
            Assert.Equal(atTheBottom[i], tooLittle[i], 12);
        }
    }

    /// <summary>A rate of nought or below falls back to the one a card usually runs at.</summary>
    [Fact]
    public void A_rate_of_nought_falls_back_to_a_sensible_one()
    {
        var input = Tone(1000, 400);

        double[] noRate = Run(new ToneFilter(1000, 0.5, 0), input);
        double[] negativeRate = Run(new ToneFilter(1000, 0.5, -48000), input);
        double[] realRate = Run(new ToneFilter(1000, 0.5, 44100), input);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(realRate[i], noRate[i], 12);
            Assert.Equal(realRate[i], negativeRate[i], 12);
        }
    }

    /// <summary>Silence in is silence out, exactly, and it does not wander off on its own.</summary>
    [Fact]
    public void A_fixed_filter_given_silence_gives_silence()
    {
        IToneFilter filter = new ToneFilter(1000, ToneFilter.MaxResonance, Rate);

        foreach (double sample in Run(filter, Steady(0, Settled)))
            Assert.Equal(0.0, sample);
    }

    /// <summary>A steady value comes out as itself: a low pass passes what does not move.</summary>
    [Fact]
    public void A_fixed_filter_settles_on_a_steady_value()
    {
        double[] output = Run(new ToneFilter(500, 0, Rate), Steady(1.0, Settled));

        Assert.Equal(1.0, output[^1], 4);
    }

    /// <summary>
    /// Thousands of samples of full scale at the top of the resonance stay finite and stay
    /// somewhere near where they started.
    /// </summary>
    /// <remarks>
    /// The tone is at the cutoff, which is the one frequency a resonant filter is going to make
    /// the most of. What it should come to is about the resonance's own Q, which is seven times
    /// the input; a filter going unstable would leave the bound long before the block ended.
    /// </remarks>
    [Fact]
    public void A_fixed_filter_at_full_resonance_stays_bounded()
    {
        double[] output = Run(new ToneFilter(1000, ToneFilter.MaxResonance, Rate), Tone(1000, Settled));

        foreach (double sample in output)
        {
            Assert.True(double.IsFinite(sample));
            Assert.True(Math.Abs(sample) < 20);
        }
    }

    /// <summary>
    /// A resonance that is not a number reads as none, exactly as a cutoff reads as wide open.
    /// </summary>
    /// <remarks>
    /// It used to poison every sample the filter would ever produce. The constructor read a NaN
    /// cutoff as wide open and said so in its own remarks, and then handed the resonance
    /// straight to a clamp, which propagates NaN by design, so all three coefficients came out
    /// NaN and the voice was silent for the whole of its life.
    /// <see cref="ISweepFilter.Set"/> has always guarded both halves.
    /// </remarks>
    [Fact]
    public void A_resonance_that_is_not_a_number_reads_as_none()
    {
        IToneFilter filter = new ToneFilter(1000, double.NaN, Rate);
        IToneFilter plain = new ToneFilter(1000, ToneFilter.MinResonance, Rate);

        Assert.False(filter.IsOpen);

        foreach (double sample in new[] { 1.0, 0.5, -0.25, 0.0 })
            Assert.Equal(plain.Process(sample), filter.Process(sample), 12);
    }

    /// <summary>Emptying the integrators is the only way a poisoned fixed filter recovers.</summary>
    /// <remarks>
    /// The coefficients are fixed and are untouched by this: what a reset clears is the memory,
    /// so a filter handed one value that is not a number is usable again afterwards rather than
    /// silent until the note ends.
    /// </remarks>
    [Fact]
    public void A_reset_empties_the_filters_memory()
    {
        IToneFilter filter = new ToneFilter(1000, 0.5, Rate);

        Assert.True(double.IsNaN(filter.Process(double.NaN)));
        Assert.True(double.IsNaN(filter.Process(0.5)));

        filter.Reset();

        double first = filter.Process(0.5);
        Assert.True(double.IsFinite(first));

        IToneFilter fresh = new ToneFilter(1000, 0.5, Rate);
        Assert.Equal(fresh.Process(0.5), first, 12);
    }

    /// <summary>A reset on a filter that is out of the way is allowed and does nothing.</summary>
    [Fact]
    public void A_reset_on_an_open_filter_changes_nothing()
    {
        IToneFilter filter = new ToneFilter(ToneFilter.OpenHz, 0, Rate);

        Assert.True(filter.IsOpen);
        filter.Reset();

        Assert.Equal(0.75, filter.Process(0.75), 12);
    }

    /// <summary>One bad sample stays in a fixed filter for the rest of its life.</summary>
    /// <remarks>
    /// This records what the code does today rather than what it should do. The state is fed
    /// back into itself, so a NaN handed in once is in both integrators from then on, and
    /// <see cref="IToneFilter"/> has nothing on it that clears them. A voice that is handed one
    /// bad sample goes silent and stays silent. The sweeping filter has
    /// <see cref="ISweepFilter.Reset"/> and recovers, which is the test below this one.
    /// </remarks>
    [Fact]
    public void One_bad_sample_stays_in_a_fixed_filter_for_ever()
    {
        IToneFilter filter = new ToneFilter(1000, 0.5, Rate);

        Assert.True(double.IsFinite(filter.Process(0.5)));
        Assert.True(double.IsNaN(filter.Process(double.NaN)));

        for (int i = 0; i < 100; i++)
            Assert.True(double.IsNaN(filter.Process(0.5)));
    }

    /// <summary>A sweeping filter takes a steady value through and lands on it.</summary>
    [Fact]
    public void A_sweeping_filter_settles_on_a_steady_value()
    {
        ISweepFilter filter = new SweepFilter(Rate);

        double[] output = Run(filter, Steady(1.0, Settled), FilterMode.LowPass);

        Assert.Equal(1.0, output[^1], 4);
    }

    /// <summary>
    /// The low pass keeps the part that does not move and the high pass throws it away, which
    /// is the whole of what the two ends mean.
    /// </summary>
    [Fact]
    public void The_two_ends_of_a_sweeping_filter_disagree_about_a_steady_value()
    {
        ISweepFilter low = new SweepFilter(Rate);
        low.Set(500, 0);

        ISweepFilter high = new SweepFilter(Rate);
        high.Set(500, 0);

        Assert.Equal(1.0, Run(low, Steady(1.0, Settled), FilterMode.LowPass)[^1], 4);
        Assert.Equal(0.0, Run(high, Steady(1.0, Settled), FilterMode.HighPass)[^1], 4);
    }

    /// <summary>The cutoff can be moved part way through a block and the sound follows it.</summary>
    /// <remarks>
    /// The coefficients are worked out every sixteenth sample as samples go through, so a move
    /// lands within a third of a millisecond. Read as a level rather than as a coefficient,
    /// since a caller can only hear the one.
    /// </remarks>
    [Fact]
    public void The_cutoff_can_be_moved_while_it_runs()
    {
        ISweepFilter filter = new SweepFilter(Rate);
        var input = Tone(2000, 2000);

        double wideOpen = Peak(Run(filter, input, FilterMode.LowPass), 1000);

        filter.Set(200, 0);
        double closed = Peak(Run(filter, input, FilterMode.LowPass), 1000);

        Assert.True(closed < wideOpen * 0.5);
    }

    /// <summary>Settings that are not numbers read as wide open rather than as coefficients.</summary>
    /// <remarks>
    /// Both halves are guarded here, unlike the fixed filter. A filter set to nonsense produces
    /// exactly what a filter nobody had touched produces.
    /// </remarks>
    [Fact]
    public void Nonsense_settings_read_as_wide_open()
    {
        ISweepFilter nonsense = new SweepFilter(Rate);
        nonsense.Set(double.NaN, double.NaN);

        ISweepFilter untouched = new SweepFilter(Rate);

        var input = Tone(1000, 400);
        double[] fromNonsense = Run(nonsense, input, FilterMode.LowPass);
        double[] fromUntouched = Run(untouched, input, FilterMode.LowPass);

        for (int i = 0; i < input.Length; i++)
            Assert.Equal(fromUntouched[i], fromNonsense[i], 12);
    }

    /// <summary>
    /// A cutoff or a resonance past either end is held there, so a lane or a knob driven off
    /// the end of its range sounds like the end of the range.
    /// </summary>
    [Fact]
    public void A_sweeping_filter_holds_both_controls_inside_their_ends()
    {
        var input = Tone(1000, 400);

        Assert.Equal(
            Settings(0, 0, input),
            Settings(ToneFilter.MinHz, 0, input));

        Assert.Equal(
            Settings(1e9, 0, input),
            Settings(ToneFilter.OpenHz, 0, input));

        Assert.Equal(
            Settings(1000, 5, input),
            Settings(1000, ToneFilter.MaxResonance, input));

        Assert.Equal(
            Settings(1000, -5, input),
            Settings(1000, ToneFilter.MinResonance, input));
    }

    /// <summary>What a sweeping filter at one setting makes of a block, for comparing two settings.</summary>
    /// <param name="cutoffHz">Where to put the cutoff.</param>
    /// <param name="resonance">And the resonance.</param>
    /// <param name="input">The block to run through.</param>
    private static double[] Settings(double cutoffHz, double resonance, double[] input)
    {
        ISweepFilter filter = new SweepFilter(Rate);
        filter.Set(cutoffHz, resonance);

        return Run(filter, input, FilterMode.LowPass);
    }

    /// <summary>A sweeping filter at a rate of nought falls back the same way the fixed one does.</summary>
    [Fact]
    public void A_sweeping_filter_at_a_rate_of_nought_falls_back()
    {
        var input = Tone(1000, 400);

        ISweepFilter noRate = new SweepFilter(0);
        noRate.Set(1000, 0.5);

        ISweepFilter realRate = new SweepFilter(44100);
        realRate.Set(1000, 0.5);

        double[] fromNoRate = Run(noRate, input, FilterMode.LowPass);
        double[] fromRealRate = Run(realRate, input, FilterMode.LowPass);

        for (int i = 0; i < input.Length; i++)
            Assert.Equal(fromRealRate[i], fromNoRate[i], 12);
    }

    /// <summary>Silence into a sweeping filter is silence out of both its ends.</summary>
    [Fact]
    public void A_sweeping_filter_given_silence_gives_silence()
    {
        ISweepFilter low = new SweepFilter(Rate);
        low.Set(1000, ToneFilter.MaxResonance);

        ISweepFilter high = new SweepFilter(Rate);
        high.Set(1000, ToneFilter.MaxResonance);

        foreach (double sample in Run(low, Steady(0, Settled), FilterMode.LowPass))
            Assert.Equal(0.0, sample);

        foreach (double sample in Run(high, Steady(0, Settled), FilterMode.HighPass))
            Assert.Equal(0.0, sample);
    }

    /// <summary>Full scale at the top of the resonance stays finite and stays bounded.</summary>
    [Fact]
    public void A_sweeping_filter_at_full_resonance_stays_bounded()
    {
        ISweepFilter filter = new SweepFilter(Rate);
        filter.Set(1000, ToneFilter.MaxResonance);

        foreach (double sample in Run(filter, Tone(1000, Settled * 2), FilterMode.LowPass))
        {
            Assert.True(double.IsFinite(sample));
            Assert.True(Math.Abs(sample) < 20);
        }
    }

    /// <summary>A sweeping filter handed one bad sample can be started again.</summary>
    /// <remarks>
    /// The state is poisoned exactly as the fixed filter's is; the difference is that there is
    /// something on the contract that clears it, so a voice being started again is a voice that
    /// works. This is what <see cref="One_bad_sample_stays_in_a_fixed_filter_for_ever"/> has no
    /// answer to.
    /// </remarks>
    [Fact]
    public void A_sweeping_filter_recovers_from_a_bad_sample_when_it_is_reset()
    {
        ISweepFilter filter = new SweepFilter(Rate);
        filter.Set(1000, 0.5);

        Assert.True(double.IsFinite(filter.Process(0.5, FilterMode.LowPass)));
        Assert.True(double.IsNaN(filter.Process(double.NaN, FilterMode.LowPass)));
        Assert.True(double.IsNaN(filter.Process(0.5, FilterMode.LowPass)));

        filter.Reset();

        Assert.True(double.IsFinite(filter.Process(0.5, FilterMode.LowPass)));
    }

    /// <summary>Both stages of a ladder pass a steady value, so the pair has a gain of one at rest.</summary>
    [Fact]
    public void A_ladder_settles_on_a_steady_value()
    {
        ILadderFilter filter = new LadderFilter(Rate);
        filter.Set(1000, 0);

        Assert.Equal(1.0, Run(filter, Steady(1.0, Settled))[^1], 4);
    }

    /// <summary>Two stages take a tone above the cutoff down much further than one does.</summary>
    /// <remarks>
    /// That is the whole reason for cascading them: four poles rather than two, which is the
    /// twenty four decibels an octave the chip this is modelled on gave.
    /// </remarks>
    [Fact]
    public void A_ladder_rolls_off_harder_than_one_stage()
    {
        var input = Tone(2000, Settled);

        ISweepFilter single = new SweepFilter(Rate);
        single.Set(500, 0);

        ILadderFilter ladder = new LadderFilter(Rate);
        ladder.Set(500, 0);

        double onStage = Peak(Run(single, input, FilterMode.LowPass), Settled / 2);
        double onLadder = Peak(Run(ladder, input), Settled / 2);

        Assert.True(onLadder < onStage * 0.5);
        Assert.True(onLadder > 0);
    }

    /// <summary>
    /// A ladder at the top of its resonance stays bounded over thousands of samples of full
    /// scale at its own cutoff.
    /// </summary>
    /// <remarks>
    /// This is the one a four pole filter is famous for getting wrong. The resonance is on the
    /// first stage alone, which is why: a pair of ringing filters at one frequency multiply
    /// into a peak that swamps everything under it.
    /// </remarks>
    [Fact]
    public void A_ladder_at_full_resonance_stays_bounded()
    {
        ILadderFilter filter = new LadderFilter(Rate);
        filter.Set(1000, ToneFilter.MaxResonance);

        foreach (double sample in Run(filter, Tone(1000, Settled * 2)))
        {
            Assert.True(double.IsFinite(sample));
            Assert.True(Math.Abs(sample) < 20);
        }
    }

    /// <summary>Silence into a ladder is silence out of it.</summary>
    [Fact]
    public void A_ladder_given_silence_gives_silence()
    {
        ILadderFilter filter = new LadderFilter(Rate);
        filter.Set(1000, ToneFilter.MaxResonance);

        foreach (double sample in Run(filter, Steady(0, Settled)))
            Assert.Equal(0.0, sample);
    }

    /// <summary>Reset really clears both stages, not only the first one.</summary>
    /// <remarks>
    /// Rung hard first, so there is something in both to forget. A stage left holding its own
    /// state would keep ringing into the silence that follows.
    /// </remarks>
    [Fact]
    public void Resetting_a_ladder_clears_both_stages()
    {
        ILadderFilter filter = new LadderFilter(Rate);
        filter.Set(1000, ToneFilter.MaxResonance);

        Run(filter, Tone(1000, Settled));
        filter.Reset();

        foreach (double sample in Run(filter, Steady(0, 500)))
            Assert.Equal(0.0, sample);
    }

    /// <summary>Settings that are not numbers leave a ladder working rather than silent.</summary>
    /// <remarks>
    /// Both stages are sweeping filters, which guard their own settings, so the ladder inherits
    /// the guard. This is the same case the fixed filter gets wrong.
    /// </remarks>
    [Fact]
    public void Nonsense_settings_leave_a_ladder_working()
    {
        ILadderFilter filter = new LadderFilter(Rate);
        filter.Set(double.NaN, double.NaN);

        foreach (double sample in Run(filter, Steady(1.0, 200)))
            Assert.True(double.IsFinite(sample));
    }

    /// <summary>The fixed attack is on the contract as well as on the class.</summary>
    [Fact]
    public void The_duckers_attack_is_named_on_the_contract()
    {
        IDucker ducker = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        Assert.Equal(Ducker.AttackMs, ducker.AttackMs, 12);
    }

    /// <summary>The follower climbs towards the key and never goes past it.</summary>
    [Fact]
    public void The_follower_climbs_towards_the_key_and_stops_there()
    {
        IDucker ducker = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        double last = 0;

        for (int i = 0; i < 4000; i++)
        {
            double level = ducker.Next(1.0);

            Assert.True(level >= last);
            Assert.True(level <= 1.0);

            last = level;
        }

        Assert.Equal(1.0, ducker.Level, 3);
    }

    /// <summary>
    /// And it falls back to exactly nought once the key goes quiet, without ever going below it.
    /// </summary>
    /// <remarks>
    /// A one pole follower approaches nought and never arrives, so without the floor a track
    /// would sit very slightly down for the rest of the session for no reason anybody can hear.
    /// </remarks>
    [Fact]
    public void The_follower_falls_all_the_way_back_to_nought()
    {
        IDucker ducker = new Ducker(TrackMix.MinDuckReleaseMs, Rate);

        for (int i = 0; i < 4000; i++) ducker.Next(1.0);

        double last = ducker.Level;

        for (int i = 0; i < 40000; i++)
        {
            double level = ducker.Next(0.0);

            Assert.True(level >= 0);
            Assert.True(level <= last);

            last = level;
        }

        Assert.Equal(0.0, ducker.Level);
    }

    /// <summary>The attack is far quicker than the release, which is what makes a duck breathe.</summary>
    [Fact]
    public void The_attack_is_quicker_than_the_release()
    {
        IDucker rising = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);
        double up = rising.Next(1.0);

        IDucker falling = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);
        for (int i = 0; i < 8000; i++) falling.Next(1.0);

        double from = falling.Level;
        double down = from - falling.Next(0.0);

        Assert.True(up > down * 10);
    }

    /// <summary>Reset really puts it back, and it stays back.</summary>
    [Fact]
    public void Resetting_the_ducker_really_resets_it()
    {
        IDucker ducker = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        for (int i = 0; i < 4000; i++) ducker.Next(1.0);
        Assert.True(ducker.Level > 0.5);

        ducker.Reset();

        Assert.Equal(0.0, ducker.Level);
        Assert.Equal(0.0, ducker.Next(0.0));
    }

    /// <summary>The key is read as a magnitude, so which way up the waveform is does not matter.</summary>
    [Fact]
    public void The_key_is_read_as_a_magnitude()
    {
        IDucker up = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);
        IDucker down = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        for (int i = 0; i < 500; i++)
        {
            up.Next(0.8);
            down.Next(-0.8);
        }

        Assert.Equal(up.Level, down.Level, 12);
    }

    /// <summary>A key past full scale is held at full scale, however far past it is.</summary>
    [Fact]
    public void A_key_past_full_scale_is_held_there()
    {
        IDucker huge = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);
        IDucker endless = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);
        IDucker full = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        for (int i = 0; i < 500; i++)
        {
            huge.Next(50);
            endless.Next(double.PositiveInfinity);
            full.Next(1.0);
        }

        Assert.Equal(full.Level, huge.Level, 12);
        Assert.Equal(full.Level, endless.Level, 12);
    }

    /// <summary>A key that is not a number reads as silence rather than poisoning the follower.</summary>
    [Fact]
    public void A_key_that_is_not_a_number_reads_as_silence()
    {
        IDucker ducker = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        for (int i = 0; i < 500; i++) ducker.Next(1.0);
        double before = ducker.Level;

        double after = ducker.Next(double.NaN);

        Assert.True(double.IsFinite(after));
        Assert.True(after < before);
    }

    /// <summary>
    /// A key quieter than about two percent of full scale ducks in proportion, like any other.
    /// </summary>
    /// <remarks>
    /// It used to duck nothing at all. The floor that stops the follower creeping towards
    /// nought for ever was applied on the way up as well as on the way down, and one attack
    /// step from nought towards a target of 0.02 is smaller than the floor, so the follower was
    /// put back to nought on every frame for ever and a quiet key track keyed nothing, silently.
    /// A key at half scale was unaffected, which is what made it hard to see.
    /// </remarks>
    [Fact]
    public void A_key_below_the_floor_still_ducks()
    {
        IDucker quiet = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        for (int i = 0; i < 10000; i++) quiet.Next(0.02);

        Assert.Equal(0.02, quiet.Level, 3);

        IDucker loud = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        for (int i = 0; i < 10000; i++) loud.Next(0.5);

        Assert.Equal(0.5, loud.Level, 3);

        IDucker silent = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        for (int i = 0; i < 500; i++) silent.Next(1.0);
        for (int i = 0; i < 200000; i++) silent.Next(0);

        Assert.Equal(0.0, silent.Level);
    }

    /// <summary>The release is held inside the strip's own ends, and nonsense reads as the default.</summary>
    [Fact]
    public void The_release_is_held_inside_the_strips_ends()
    {
        IDucker ducker = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        ducker.ReleaseMs = 0;
        Assert.Equal(TrackMix.MinDuckReleaseMs, ducker.ReleaseMs, 12);

        ducker.ReleaseMs = -500;
        Assert.Equal(TrackMix.MinDuckReleaseMs, ducker.ReleaseMs, 12);

        ducker.ReleaseMs = 100000;
        Assert.Equal(TrackMix.MaxDuckReleaseMs, ducker.ReleaseMs, 12);

        ducker.ReleaseMs = double.NaN;
        Assert.Equal(TrackMix.DefaultDuckReleaseMs, ducker.ReleaseMs, 12);

        Assert.Equal(TrackMix.MinDuckReleaseMs, new Ducker(0, Rate).ReleaseMs, 12);
        Assert.Equal(TrackMix.MaxDuckReleaseMs, new Ducker(1e9, Rate).ReleaseMs, 12);
        Assert.Equal(TrackMix.DefaultDuckReleaseMs, new Ducker(double.NaN, Rate).ReleaseMs, 12);
    }

    /// <summary>A longer release really does take longer to come back up.</summary>
    [Fact]
    public void A_longer_release_takes_longer_to_come_back()
    {
        IDucker quick = new Ducker(TrackMix.MinDuckReleaseMs, Rate);
        IDucker slow = new Ducker(TrackMix.MaxDuckReleaseMs, Rate);

        for (int i = 0; i < 4000; i++)
        {
            quick.Next(1.0);
            slow.Next(1.0);
        }

        for (int i = 0; i < 4000; i++)
        {
            quick.Next(0.0);
            slow.Next(0.0);
        }

        Assert.True(quick.Level < slow.Level);
    }

    /// <summary>A ducker at a rate of nought falls back the same way the filters do.</summary>
    [Fact]
    public void A_ducker_at_a_rate_of_nought_falls_back()
    {
        IDucker noRate = new Ducker(TrackMix.DefaultDuckReleaseMs, 0);
        IDucker realRate = new Ducker(TrackMix.DefaultDuckReleaseMs, 44100);

        for (int i = 0; i < 500; i++)
        {
            noRate.Next(1.0);
            realRate.Next(1.0);
        }

        Assert.Equal(realRate.Level, noRate.Level, 12);
    }

    /// <summary>Nothing ducking is a gain of one and everything ducking is a gain of nought.</summary>
    [Fact]
    public void The_gain_runs_from_one_down_to_nought()
    {
        IDucker ducker = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        Assert.Equal(1.0, (double)ducker.GainFor(0, 1), 6);
        Assert.Equal(0.0, (double)ducker.GainFor(1, 1), 6);
        Assert.Equal(0.5, (double)ducker.GainFor(1, 0.5), 6);
        Assert.Equal(0.75, (double)ducker.GainFor(0.5, 0.5), 6);
        Assert.Equal(1.0, (double)ducker.GainFor(1, 0), 6);
    }

    /// <summary>The follower and the depth are both held inside nought and one before they meet.</summary>
    [Fact]
    public void The_gain_holds_both_of_its_arguments_inside_their_ends()
    {
        IDucker ducker = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        Assert.Equal(0.0, (double)ducker.GainFor(5, 5), 6);
        Assert.Equal(1.0, (double)ducker.GainFor(-5, 1), 6);
        Assert.Equal(1.0, (double)ducker.GainFor(1, -5), 6);
        Assert.Equal(0.0, (double)ducker.GainFor(double.PositiveInfinity, 1), 6);
    }

    /// <summary>Either argument being not a number at all reads as no ducking.</summary>
    /// <remarks>
    /// It used to make a gain that was not a number. <see cref="IDucker.Next"/> guarded its key
    /// and said so; the gain guarded neither of its two and a clamp propagates NaN by design,
    /// so a depth off a song somebody had edited multiplied a whole track by NaN and silenced
    /// it. An infinite depth is a separate question and is still held at full scale.
    /// </remarks>
    [Fact]
    public void A_gain_argument_that_is_not_a_number_reads_as_no_ducking()
    {
        IDucker ducker = new Ducker(TrackMix.DefaultDuckReleaseMs, Rate);

        Assert.Equal(1.0, (double)ducker.GainFor(1, double.NaN), 6);
        Assert.Equal(1.0, (double)ducker.GainFor(double.NaN, 1), 6);
        Assert.Equal(1.0, (double)ducker.GainFor(double.NaN, double.NaN), 6);
    }
}
