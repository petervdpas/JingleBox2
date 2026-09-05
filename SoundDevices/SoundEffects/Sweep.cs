using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <summary>
/// A resonant filter with a drive into it: what a synthesiser does to a whole track.
/// </summary>
/// <remarks>
/// The second engine, and the plan's reason for it holding up: the maths was written already,
/// per voice, in <c>Tracker/Synth/</c>. What moves across is the arithmetic and not the class,
/// because per voice and per track are not the same signal. A voice is mono and short lived and
/// gets a filter built for its own lifetime; a track is two channels that run for the length of
/// a show, so the state is per side and is made once.
///
/// **Four poles rather than two, and the drive is what makes it sing.** A resonant filter on its
/// own is a tone control that whistles. What every filter anybody wants to own has in front of
/// the resonance is something that saturates, so the peak leans on it and rounds off instead of
/// screaming: that is the whole difference between a filter sweep that sounds like an instrument
/// and one that sounds like a fault. The drive is before the poles, deliberately, since a
/// distortion after a filter is a distortion and a distortion before one is a character.
///
/// The two stages are the sweeping filter twice, which is what the ladder in the synth already
/// is: six decibels an octave apiece, so a pair is twelve and two pairs would be the twenty four
/// an Emulator gave. The resonance goes on the first stage only, and the second is there to make
/// the slope rather than to ring, or the two would ring against each other.
///
/// **Both modes are one filter read differently.** The low pass is what the poles give and the
/// high pass is what is left when it is taken from the input, which is how the sweeping filter
/// is written and is why band pass falls out for nothing: it is the low pass of the high pass,
/// so it is the second stage reading the first the other way round.
///
/// The cutoff glides for the same reason the delay's time does. A filter recomputed from a
/// jumped cutoff is a click on every message from a controller, and a hand sweeping a knob is a
/// hundred messages a second. What glides is the frequency in cents rather than in hertz, since
/// a sweep that moves evenly in hertz crawls at the bottom and leaps at the top, and the ear
/// hears octaves.
///
/// Nothing here allocates, takes a lock or blocks, which is what <see cref="ISoundEffectEngine"/>
/// asks of anything on the audio path.
/// </remarks>
public sealed class Sweep : ISoundEffectEngine
{
    /// <summary>Where the filter turns over, in hertz.</summary>
    /// <remarks>
    /// Written out rather than built, so the words this effect and its manifest have to agree on
    /// can be found by looking for them. They are the same strings <c>effect.json</c> names.
    /// </remarks>
    public const string Cutoff = "cutoff";

    /// <summary>How hard it rings at the cutoff.</summary>
    public const string Resonance = "resonance";

    /// <summary>How hard the signal is pushed into the poles.</summary>
    public const string Drive = "drive";

    /// <summary>Which way round it is read: nought low, one band, two high.</summary>
    public const string Mode = "mode";

    /// <summary>How much of what comes out is filtered rather than what went in.</summary>
    public const string Mix = "mix";

    /// <summary>Whether the poles run before the drive rather than after it.</summary>
    public const string FilterFirst = "filter_first";

    /// <summary>Whether the drive is paid for by its peak or by its loudness.</summary>
    public const string Even = "even";

    /// <summary>The lowest the cutoff goes, which is under everything anybody records.</summary>
    public const double LeastHz = 20;

    /// <summary>And the highest, which is open.</summary>
    public const double MostHz = 20000;

    /// <summary>Past this the resonance is self oscillation rather than character.</summary>
    public const double MostResonance = 0.98;

    /// <summary>The most the drive multiplies by before the poles.</summary>
    public const double MostDrive = 8;

    /// <summary>Low pass, band pass, high pass.</summary>
    public const double MostMode = 2;

    /// <summary>
    /// How far the cutoff moves towards where it was put, per block.
    /// </summary>
    /// <remarks>
    /// Per block rather than per sample, since the coefficients are worked out per block anyway
    /// and a glide finer than that is arithmetic nobody can hear. A twentieth is about a tenth
    /// of a second at an ordinary buffer, which is a hand's own speed.
    /// </remarks>
    private const double Glide = 0.05;

    /// <summary>How near counts as arrived, so the glide stops rather than approaching for ever.</summary>
    private const double Close = 0.5;

    /// <inheritdoc/>
    public string Id { get; }

    /// <summary>Every parameter, in the order a face reads them.</summary>
    private static readonly string[] Words = { Cutoff, Resonance, Drive, Mode, Mix, FilterFirst, Even };

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyList<string> Keys => Words;

    /// <summary>The rate the coefficients are worked out against.</summary>
    private readonly double _rate;

    /// <summary>The two poles of the left side, and then of the right.</summary>
    private readonly Pole[] _poles = { new(), new(), new(), new() };

    /// <summary>Where the cutoff has been put, which is where it is gliding to.</summary>
    private volatile float _cutoff = 12000;

    /// <summary>
    /// Where it has got to, which is what the coefficients are made from, and nought until it
    /// has been placed.
    /// </summary>
    /// <remarks>
    /// Nought is not a cutoff, so it is free to mean "nowhere yet": the first value written or
    /// the first block rendered puts it where it belongs rather than gliding to it. It was
    /// twelve thousand, which is a real cutoff and so could never mean that, and the effect was
    /// that a song opening swept up from wherever the default was instead of starting where it
    /// was saved.
    /// </remarks>
    private double _at;

    /// <summary>How hard it rings.</summary>
    private volatile float _resonance = 0.2f;

    /// <summary>How hard the signal is pushed in.</summary>
    private volatile float _drive = 1;

    /// <summary>Whether the poles run before the drive rather than after it.</summary>
    /// <remarks>
    /// False is what this effect has always done and is the default, so a chain written before the
    /// switch existed sounds exactly as it did. Drive first squares the wave up and the poles then
    /// take the top off what it made, which is the classic screaming filter; poles first shapes
    /// the signal and the curve rounds off what is left, which is where the resonance stops being
    /// handed something that is already square.
    /// </remarks>
    private volatile bool _filterFirst;

    /// <summary>Whether the drive is paid for by its loudness rather than by its peak.</summary>
    /// <remarks><inheritdoc cref="_filterFirst" path="/remarks/text()[1]"/></remarks>
    private volatile bool _even;

    /// <summary>What the drive is costing in loudness, measured off what has gone past.</summary>
    private readonly ILoudnessMakeup _loudness;

    /// <summary>Which way round it is read.</summary>
    private volatile float _mode;

    /// <summary>How much of the filtered signal comes out.</summary>
    private volatile float _mix = 1;

    /// <summary>Builds one at the rate it is about to be handed audio at.</summary>
    /// <param name="sampleRate">What the host is running at.</param>
    /// <param name="id">Which effect this is standing for, or nothing for one built by hand.</param>
    public Sweep(int sampleRate, string? id = null)
    {
        _rate = sampleRate <= 0 ? 44100 : sampleRate;
        _loudness = new LoudnessMakeup(sampleRate);
        Id = id ?? "";
    }

    /// <inheritdoc/>
    public double ValueOf(string? key) => key switch
    {
        Cutoff => _cutoff,
        Resonance => _resonance,
        Drive => _drive,
        FilterFirst => _filterFirst ? 1 : 0,
        Even => _even ? 1 : 0,
        Mode => _mode,
        Mix => _mix,
        _ => 0
    };

    /// <inheritdoc/>
    /// <remarks>
    /// A cutoff set before anything has been rendered is where the filter starts rather than
    /// somewhere to glide from, so a song opening is not a sweep up from wherever the last one
    /// left off.
    /// </remarks>
    public void SetValue(string? key, double value)
    {
        if (double.IsNaN(value)) return;

        switch (key)
        {
            case Cutoff:
                _cutoff = (float)Math.Clamp(value, LeastHz, MostHz);
                if (_at <= 0) _at = _cutoff;
                break;

            case Resonance:
                _resonance = (float)Math.Clamp(value, 0, MostResonance);
                break;

            case Drive:
                _drive = (float)Math.Clamp(value, 1, MostDrive);
                break;

            case Mode:
                _mode = (float)Math.Clamp(Math.Round(value), 0, MostMode);
                break;

            case FilterFirst:
                _filterFirst = value >= 0.5;
                break;

            case Even:
                _even = value >= 0.5;
                break;

            case Mix:
                _mix = (float)Math.Clamp(value, 0, 1);
                break;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The block is held to what the buffer can really take and rounded down to whole frames,
    /// the same rule the mixer keeps: a caller claiming more frames than it handed over is what
    /// writing past the end of somebody's array looks like from in here.
    ///
    /// **It runs whatever its knobs say, including when they say nothing.** There were two fast
    /// paths here, one for no mix and one for wide open and undriven, and both were a tick
    /// waiting to happen: a block skipped is a block the poles did not see, so their memory is
    /// from whenever the effect was last doing something and the first block after it re-engages
    /// starts from that instead of from the signal. A knob crossing the threshold is then a jump
    /// rather than a fade, which is heard as a click and is the sort of thing nobody can find
    /// afterwards.
    ///
    /// The mix is a crossfade and does the job the fast path was reaching for: at nought the
    /// filtered signal contributes nothing and the poles go on hearing the input, so coming off
    /// nought is a fade from where the sound really is. Four poles a sample a side is not a cost
    /// worth a class of clicks.
    /// </remarks>
    /// <param name="buffer">Interleaved stereo, worked on in place.</param>
    /// <param name="frames">How many frames of it are real.</param>
    public void Process(float[] buffer, int frames)
    {
        if (buffer == null || frames <= 0) return;

        int count = Math.Min(frames, buffer.Length / 2);

        if (count <= 0) return;

        double mix = _mix;

        Slide();

        double drive = _drive;
        bool even = _even;
        bool first = _filterFirst;
        double makeup = even ? 1 : drive > 1 ? 1.0 / Math.Tanh(drive) : 1;
        int mode = (int)_mode;

        for (int frame = 0; frame < count; frame++)
        {
            int at = frame * 2;

            if (even) makeup = _loudness.Makeup;

            buffer[at] = (float)One(buffer[at], 0, drive, makeup, mode, mix, first, even);
            buffer[at + 1] = (float)One(buffer[at + 1], 2, drive, makeup, mode, mix, first, even);
        }
    }

    /// <summary>
    /// One sample of one side: driven, filtered, read the way the mode asks, and mixed back.
    /// </summary>
    /// <param name="sample">What went in.</param>
    /// <param name="side">Which pair of poles, 0 for the left and 2 for the right.</param>
    /// <param name="drive">How hard it is pushed into them.</param>
    /// <param name="makeup">What that drive costs in level, undone.</param>
    /// <param name="mode">Nought low, one band, two high.</param>
    /// <param name="mix">How much of the result comes out.</param>
    /// <param name="filterFirst">Whether the poles run before the curve rather than after it.</param>
    /// <param name="even">Whether the loudness follower is being kept, which only that makeup needs.</param>
    /// <remarks>
    /// The three modes are the two stages read one way or the other, and the middle one is why
    /// there is no third filter: low then high is a band, since taking the bottom off something
    /// that has already had its top taken off leaves the middle. High wants **both** stages read
    /// high, which is the one that was wrong first: high then low is a band again, so the high
    /// pass and the band pass were the same filter under two names.
    ///
    /// The order switch is one branch and not two code paths. The poles are run over whatever they
    /// are given and the curve is applied to whatever it is given, so which is handed the sample
    /// and which is handed the other's answer is the whole of the difference.
    /// </remarks>
    private double One(double sample, int side, double drive, double makeup, int mode, double mix,
                       bool filterFirst, bool even)
    {
        double wet;

        if (filterFirst)
        {
            double filtered = Poles(sample, side, mode);

            wet = Driven(filtered, drive, makeup, even);
        }
        else
        {
            wet = Poles(Driven(sample, drive, makeup, even), side, mode);
        }

        return mix >= 1 ? wet : sample + (wet - sample) * mix;
    }

    /// <summary>The two poles of one side, read the way the mode asks.</summary>
    /// <param name="sample">What goes into them.</param>
    /// <param name="side">Which pair, 0 for the left and 2 for the right.</param>
    /// <param name="mode">Nought low, one band, two high.</param>
    private double Poles(double sample, int side, int mode)
    {
        double first = _poles[side].Run(sample, _a1, _a2, _a3, _k, mode == 2);

        return _poles[side + 1].Run(first, _a1, _a2, _a3, _k, mode >= 1);
    }

    /// <summary>The curve, and the follower that measures what it cost.</summary>
    /// <remarks>
    /// The follower is fed with the curve's own two ends rather than with the effect's, so it
    /// answers what the drive did and not what the filter did. That is what makes the switch mean
    /// the same thing whichever order the two are in.
    /// </remarks>
    /// <param name="sample">What goes into the curve.</param>
    /// <param name="drive">How hard it is pushed. One is no drive at all.</param>
    /// <param name="makeup">What that costs in level, undone.</param>
    /// <param name="even">Whether the follower is being kept.</param>
    private double Driven(double sample, double drive, double makeup, bool even)
    {
        if (drive <= 1) return sample;

        double bitten = Math.Tanh(sample * drive);

        if (even) _loudness.Saw(sample, bitten);

        return bitten * makeup;
    }

    /// <summary>The three coefficients and the damping the poles are run with.</summary>
    private double _a1 = 1, _a2, _a3, _k = 2;

    /// <summary>
    /// Moves the cutoff towards where it was put, and works the coefficients out again.
    /// </summary>
    /// <remarks>
    /// In cents rather than in hertz. A sweep that moves evenly in hertz crawls through the two
    /// octaves anybody is listening to and then leaps through the eight nobody is, which is the
    /// difference between a filter that follows a hand and one that jumps at the top.
    /// </remarks>
    private void Slide()
    {
        double target = _cutoff;

        if (_at <= 0) _at = target;

        double ratio = target / _at;

        if (Math.Abs(_at - target) <= Close) _at = target;
        else _at *= Math.Pow(ratio, Glide);

        double cutoff = Math.Clamp(_at, LeastHz, Math.Min(MostHz, _rate * 0.49));

        double g = Math.Tan(Math.PI * cutoff / _rate);

        _k = 2.0 - 1.9 * _resonance;
        _a1 = 1.0 / (1.0 + g * (g + _k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    /// <summary>
    /// One pole pair's memory, and the two numbers that are all it is.
    /// </summary>
    /// <remarks>
    /// A class rather than four loose fields, because there are four of them and naming them
    /// left first, left second, right first and right second is how one of them comes to be
    /// updated and the other not.
    /// </remarks>
    private sealed class Pole
    {
        /// <summary>The first integrator.</summary>
        private double _first;

        /// <summary>The second, which is the low pass output.</summary>
        private double _second;

        /// <summary>
        /// Runs one sample through, answering the low pass or the high pass.
        /// </summary>
        /// <param name="input">What goes in.</param>
        /// <param name="a1">The first coefficient.</param>
        /// <param name="a2">The second.</param>
        /// <param name="a3">The third.</param>
        /// <param name="k">The damping, which is what the resonance really sets.</param>
        /// <param name="high">True for what is left when the low pass is taken away.</param>
        public double Run(double input, double a1, double a2, double a3, double k, bool high)
        {
            double third = input - _second;
            double v1 = a1 * _first + a2 * third;
            double v2 = _second + a2 * _first + a3 * third;

            _first = 2.0 * v1 - _first;
            _second = 2.0 * v2 - _second;

            return high ? input - k * v1 - v2 : v2;
        }
    }
}
