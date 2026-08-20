using System;
using System.Globalization;

namespace JingleBox2.Tracker;

/// <summary>
/// One step on one track: what to play, with what, how loud, and one effect.
/// Every column is independently blank, so a cell can change volume without retriggering.
/// </summary>
public readonly record struct TrackerCell(Note Note, int Instrument, int Volume, TrackerEffect Effect)
{
    public const int NoInstrument = -1;
    public const int NoVolume = -1;

    /// <summary>Full volume, in the classic 0-64 tracker scale.</summary>
    public const int MaxVolume = 64;

    public static readonly TrackerCell Empty =
        new(Note.Empty, NoInstrument, NoVolume, TrackerEffect.None);

    public bool IsEmpty =>
        Note.IsEmpty && Instrument == NoInstrument && Volume == NoVolume && Effect.IsNone;

    /// <summary>True when this cell should start a voice, as opposed to only adjusting one.</summary>
    public bool Triggers => Note.IsPlayable;

    public static int ClampVolume(int volume) =>
        volume == NoVolume ? NoVolume : Math.Clamp(volume, 0, MaxVolume);

    /// <summary>Volume as a 0-1 gain, or null when the column is blank.</summary>
    public float? Gain => Volume == NoVolume ? null : Math.Clamp(Volume, 0, MaxVolume) / (float)MaxVolume;

    public string InstrumentText =>
        Instrument == NoInstrument ? ".." : Instrument.ToString("00", CultureInfo.InvariantCulture);

    public string VolumeText =>
        Volume == NoVolume ? ".." : Volume.ToString("X2", CultureInfo.InvariantCulture);

    public override string ToString() =>
        $"{Note} {InstrumentText} {VolumeText} {Effect}";
}
