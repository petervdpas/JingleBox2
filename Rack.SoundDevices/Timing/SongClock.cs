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

        Set(was with { Beats = was.Beats + frames / (double)sampleRate * was.Bpm / 60.0 });
    }

    /// <summary>A tempo a clock can be run at, whatever it was handed.</summary>
    private static double Sensible(double bpm) =>
        double.IsFinite(bpm) && bpm > 1.0 ? bpm : 120.0;
}
