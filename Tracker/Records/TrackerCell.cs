using System;
using System.Globalization;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// One step on one track: what to play, with what, how loud, and one effect.
/// Every column is independently blank, so a cell can change volume without retriggering.
/// </summary>
/// <param name="Note">What to play, or <see cref="Note.Empty"/>, or <see cref="Note.Off"/>.</param>
/// <param name="Instrument">
/// Which instrument, or <see cref="NoInstrument"/>, which the sequencer reads as "whatever this
/// track last played" rather than as an error.
/// </param>
/// <param name="Volume">How loud, 0 to <see cref="MaxVolume"/>, or <see cref="NoVolume"/>.</param>
/// <param name="Effect">One effect command, or <see cref="TrackerEffect.None"/>.</param>
public readonly record struct TrackerCell(Note Note, int Instrument, int Volume, TrackerEffect Effect)
{
    /// <summary>A blank instrument column. Not an error: the track's own instrument is used.</summary>
    public const int NoInstrument = -1;

    /// <summary>A blank volume column, which leaves the instrument's own level to decide.</summary>
    public const int NoVolume = -1;

    /// <summary>
    /// Full volume, which is 0x80 and is written as two hex digits like everything else here.
    /// </summary>
    /// <remarks>
    /// 128 rather than the 64 a tracker has had since FastTracker, because MIDI has 128
    /// velocities and the old scale could hold only half of them: two keys struck a little
    /// apart wrote the same number and a hit at full read 40. A velocity is written in
    /// unchanged now, so the pattern shows what the keyboard sent and 0x80 is the one level
    /// above anything a key can produce, reached by typing it.
    ///
    /// Songs written on the old scale are doubled on the way in, which is exact. See
    /// <see cref="Interfaces.IVolumeScale"/>.
    /// </remarks>
    public const int MaxVolume = 128;

    /// <summary>Every column blank, which is what a pattern is filled with.</summary>
    public static readonly TrackerCell Empty =
        new(Note.Empty, NoInstrument, NoVolume, TrackerEffect.None);

    /// <summary>True when every column is blank, so the file need not store it.</summary>
    public bool IsEmpty =>
        Note.IsEmpty && Instrument == NoInstrument && Volume == NoVolume && Effect.IsNone;

    /// <summary>True when this cell should start a voice, as opposed to only adjusting one.</summary>
    public bool Triggers => Note.IsPlayable;

    /// <summary>Holds a volume inside the scale, leaving a blank column blank.</summary>
    public static int ClampVolume(int volume) =>
        volume == NoVolume ? NoVolume : Math.Clamp(volume, 0, MaxVolume);

    /// <summary>Volume as a 0-1 gain, or null when the column is blank.</summary>
    public float? Gain => Volume == NoVolume ? null : Math.Clamp(Volume, 0, MaxVolume) / (float)MaxVolume;

    /// <summary>The instrument column as two decimal digits, or ".." when blank.</summary>
    public string InstrumentText =>
        Instrument == NoInstrument ? ".." : Instrument.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>The volume column as two hex digits, or ".." when blank.</summary>
    public string VolumeText =>
        Volume == NoVolume ? ".." : Volume.ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>The four columns with a space between each, as the grid and the file write them.</summary>
    public override string ToString() =>
        $"{Note} {InstrumentText} {VolumeText} {Effect}";
}
