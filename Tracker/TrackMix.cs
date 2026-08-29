using System;

namespace JingleBox2.Tracker;

/// <summary>One track's place in the mix: how loud, how wide, and whether it is heard at all.</summary>
public sealed class TrackMix
{
    /// <summary>Silence, which is what a fader pulled all the way down is.</summary>
    public const double MinVolume = 0;

    /// <summary>
    /// Past unity, so a quiet track can be pushed up rather than everything else down. Stored
    /// as an amplitude, because that is what the engine multiplies by; the fader reads it in
    /// decibels, where this is +6.
    /// </summary>
    public const double MaxVolume = 2;

    /// <summary>Unity, which is where a new strip sits and what a song written before the mixer opens at.</summary>
    public const double DefaultVolume = 1;

    /// <summary>How loud, as an amplitude. The fader on the screen shows it in decibels.</summary>
    public double Volume { get; set; } = DefaultVolume;

    /// <summary>-1 hard left, 0 centre, 1 hard right.</summary>
    public double Pan { get; set; }

    /// <summary>Silences this strip whatever its fader says.</summary>
    public bool Mute { get; set; }

    /// <summary>
    /// Silences every strip that is not soloed, for as long as any strip is.
    /// </summary>
    /// <remarks>
    /// The awkward one, because it is a setting on one strip whose effect is on all the others.
    /// <see cref="MixLevels"/> is where that is worked out, once, so nothing has to remember to
    /// ask both questions.
    /// </remarks>
    public bool Solo { get; set; }

    /// <summary>No key track: this strip is not ducked by anything.</summary>
    public const int NoKey = -1;

    /// <summary>No ducking at all, which is what a strip nobody has keyed sits at.</summary>
    public const double MinDuck = 0;

    /// <summary>Full depth ducks to silence while the key track is at full scale.</summary>
    public const double MaxDuck = 1;

    /// <summary>Faster than this and coming back up is audible as a click rather than as a release.</summary>
    public const double MinDuckReleaseMs = 20;

    /// <summary>A second, past which a track ducked on a four to the floor never comes back at all.</summary>
    public const double MaxDuckReleaseMs = 1000;

    /// <summary>What a new strip is given, which is quick enough to breathe and slow enough to hear.</summary>
    public const double DefaultDuckReleaseMs = 150;

    /// <summary>How far this track is pushed down while the key track sounds. Zero is off.</summary>
    public double Duck { get; set; }

    /// <summary>The track that does the pushing, or <see cref="NoKey"/>.</summary>
    public int DuckFrom { get; set; } = NoKey;

    /// <summary>How long the track takes to come back up once the key stops.</summary>
    public double DuckReleaseMs { get; set; } = DefaultDuckReleaseMs;

    /// <summary>
    /// The effects on this track, saved with the song. Null for a track that has none, so a
    /// song file does not carry a row of empty chains.
    /// </summary>
    public Audio.Plugins.PluginChainConfig? Plugins { get; set; }

    /// <summary>
    /// A strip of its own with the same settings, chain included.
    /// </summary>
    /// <remarks>
    /// What a song being written down and a history step both take, so the copy must share
    /// nothing: the chain is cloned too, or two songs would be holding one list of effects.
    /// </remarks>
    public TrackMix Clone() => new()
    {
        Volume = Volume,
        Pan = Pan,
        Mute = Mute,
        Solo = Solo,
        Duck = Duck,
        DuckFrom = DuckFrom,
        DuckReleaseMs = DuckReleaseMs,
        Plugins = Plugins?.Clone()
    };

    /// <summary>
    /// Holds every value inside its range, and replaces a NaN with what a new strip has.
    /// </summary>
    /// <remarks>
    /// NaN is checked for by name rather than left to the clamp, because a NaN compares false
    /// against both ends and comes out of <c>Math.Clamp</c> still a NaN. One of those reaching
    /// the mixer silences the whole song, since anything multiplied by it is a NaN as well.
    /// </remarks>
    public void Clamp()
    {
        Volume = double.IsNaN(Volume) ? DefaultVolume : Math.Clamp(Volume, MinVolume, MaxVolume);
        Pan = double.IsNaN(Pan) ? 0 : Math.Clamp(Pan, -1, 1);

        Duck = double.IsNaN(Duck) ? 0 : Math.Clamp(Duck, MinDuck, MaxDuck);

        DuckReleaseMs = double.IsNaN(DuckReleaseMs)
            ? DefaultDuckReleaseMs
            : Math.Clamp(DuckReleaseMs, MinDuckReleaseMs, MaxDuckReleaseMs);
    }
}

