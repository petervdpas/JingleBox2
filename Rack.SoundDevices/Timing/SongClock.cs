using System;
using System.Diagnostics;
using System.Threading;

namespace JingleBox2.Rack.SoundDevices.Timing;

/// <summary>
/// Where the song is, as everything that makes a sound reads it.
/// </summary>
/// <remarks>
/// In the sound device kit because a tempo belongs to the song and not to any one kind of device.
/// A delay of ours wanting a dotted eighth, a machine's wobble wanting a bar, and somebody else's
/// plugin wanting either are all asking the same question, and there is one answer to it.
///
/// Static, because a transport is one thing for the whole application: there is one song playing,
/// at one tempo, and everything loaded is being played by it. Handing the same answer down
/// through the mixer, the rack, the chain, the bridge and into a device would be a parameter added
/// to a dozen signatures to carry a value that is the same everywhere it arrives.
///
/// It is also read on the far side of a process boundary. A plugin running in its own process has
/// its own copy of this, which the host process fills in from the shared block before each block
/// of audio, so the code that reads it does not have to know which side of the bridge it is on.
///
/// Written by the clock thread and by whichever thread starts or stops the transport, read by the
/// audio thread. Every write is a reference swap of an immutable record, so a reader sees one
/// settled answer rather than a half written one.
/// </remarks>
public static class SongClock
{
    /// <summary>What everything is being told, swapped whole.</summary>
    private static Transport _now = Transport.Still;

    /// <summary>
    /// Where whatever is playing the song says it has got to at a stopwatch moment, in beats since
    /// it started, or not a number where it cannot say. Null where nothing has said it will.
    /// </summary>
    private static Func<long, double>? _reference;

    /// <summary>
    /// Hands the clock something to be checked against: the player's own idea of where the song is,
    /// which is what the notes it sends are placed by.
    /// </summary>
    /// <remarks>
    /// The beat is counted in samples rendered, and that is right for as long as every sample is
    /// rendered. It stops being right the moment one is not. When the audio thread is held up long
    /// enough that the device plays silence in its place (a plugin taking fifteen blocks to answer,
    /// which is what opening a window in the same process as a plugin can cost), those samples
    /// never happen, the count never moves for them, and every plugin reading this falls behind the
    /// notes the player is sending by exactly as long as the stall lasted. For good: nothing ever
    /// put it back. A drum machine following the host ended up a sixth of a second behind the
    /// tracker after one stall, and further behind after every one that followed.
    ///
    /// The player keeps its lines against a stopwatch, which a stall does not stop. So the count is
    /// checked against it, and put right when the two have come apart. See <see cref="Reconciled"/>.
    ///
    /// Null lets go, which is what the far side of a bridge has: it is told the beat whole with
    /// every block and has nothing to check it against.
    /// </remarks>
    public static void Follow(Func<long, double>? reference) => Volatile.Write(ref _reference, reference);

    /// <summary>Where the song is. Never null.</summary>
    public static Transport Now => Volatile.Read(ref _now);

    /// <summary>Puts a whole transport in place, which is what the far side of a bridge does.</summary>
    public static void Set(Transport transport) => Volatile.Write(ref _now, transport ?? Transport.Still);

    /// <summary>The transport rolled, from the top of wherever it was told to start.</summary>
    public static void Started(double bpm, int numerator = 4, int denominator = 4) =>
        Set(new Transport(true, Sensible(bpm), 0.0, numerator, denominator));

    /// <summary>And stopped, which leaves the beat where it was rather than winding it back.</summary>
    /// <remarks>
    /// Left where it was because anything asking what beat it is while the transport sits still
    /// wants the answer it had a moment ago, not nought. Starting again is what sets it to the
    /// top, and that is <see cref="Started"/>'s business.
    /// </remarks>
    public static void Stopped()
    {
        var was = Now;

        Set(was with { Playing = false });
    }

    /// <summary>The tempo moved under a transport that may or may not be running.</summary>
    public static void Tempo(double bpm)
    {
        var was = Now;
        var wanted = Sensible(bpm);

        if (was.Bpm == wanted) return;

        Set(was with { Bpm = wanted });
    }

    /// <summary>
    /// A block of audio went by, which is the only thing that moves the beat along.
    /// </summary>
    /// <remarks>
    /// Counted in samples rendered rather than in lines played, so the number anything reads is
    /// exactly as far as the audio it is about to make. The clock thread runs ahead of the audio
    /// and a beat taken from it would be early by however far ahead it had got.
    /// </remarks>
    public static void Advance(int frames, int sampleRate)
    {
        if (frames <= 0 || sampleRate <= 0) return;

        var was = Now;

        if (!was.Playing) return;

        double counted = was.Beats + frames / (double)sampleRate * was.Bpm / 60.0;

        Set(was with { Beats = Reconciled(counted, was.Bpm, frames / (double)sampleRate) });
    }

    /// <summary>
    /// The counted beat, left alone, eased back, or put right, against what the player says.
    /// </summary>
    /// <remarks>
    /// Three answers, by how far apart the two are.
    ///
    /// **Close together, the count is kept as it is.** The player's thread runs ahead of the sound
    /// by up to a block, since a note it sends is rendered at the next block, so its answer is
    /// always a little early and never exactly the same distance early twice. Following it closely
    /// would be telling every plugin that jitter. The count is smooth to the sample, which is why
    /// it is the beat in the first place. Close means within two blocks, or a fortieth of a second
    /// where blocks are short.
    ///
    /// **A little further, the count is eased towards the player,** a hundredth of the difference a
    /// block. That is a sound card whose crystal and the computer's stopwatch do not quite agree,
    /// which every pair of clocks does, by a few parts in a hundred thousand: left alone it is a
    /// fraction of a beat an hour, and eased it is never heard.
    ///
    /// **Far apart, the count is put where the player is, at once.** That is a stall: samples that
    /// were never rendered. A plugin sees its song jump forward, which is what just happened to the
    /// song, and plays on in time with the notes rather than a stall behind them. Far means four
    /// blocks, or a twelfth of a second.
    /// </remarks>
    /// <param name="counted">The beat the samples rendered say.</param>
    /// <param name="bpm">The tempo, to turn beats into time.</param>
    /// <param name="blockSeconds">How long the block just rendered was.</param>
    private static double Reconciled(double counted, double bpm, double blockSeconds)
    {
        var reference = Volatile.Read(ref _reference);

        if (reference is null) return counted;

        double told = reference(Stopwatch.GetTimestamp());

        if (!double.IsFinite(told)) return counted;

        double secondsPerBeat = 60.0 / Math.Max(1.0, bpm);
        double apart = (told - counted) * secondsPerBeat;
        double close = Math.Max(0.025, blockSeconds * 2.0);
        double far = Math.Max(0.08, blockSeconds * 4.0);

        if (Math.Abs(apart) <= close) return counted;
        if (Math.Abs(apart) > far) return told;

        return counted + (told - counted) * 0.01;
    }

    /// <summary>A tempo a clock can be run at, whatever it was handed.</summary>
    private static double Sensible(double bpm) =>
        double.IsFinite(bpm) && bpm > 1.0 ? bpm : 120.0;
}
