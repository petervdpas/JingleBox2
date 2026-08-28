namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// One track pushing another down: the kick keying the bass, the way a desk does it with a
/// compressor and a side chain. Follows the key track's level and turns that into a gain.
/// </summary>
/// <remarks>
/// Deliberately not a compressor. There is no threshold, no ratio and no knee: the depth knob
/// says how far down the track goes when the key is at full scale, and the track follows the
/// key in proportion below that. It is the effect people actually reach for, with two controls
/// instead of five.
///
/// The attack is fixed and fast. A slow duck attack leaves the first part of the key note
/// fighting the track it is meant to be clearing room for, which is the one thing this is for.
///
/// One per strip, and <see cref="Next"/> runs on the audio thread once per frame. Nothing here
/// allocates or takes a lock; <see cref="ReleaseMs"/> is written from the block that is about
/// to render, which is why it is a property that works its coefficient out on the spot rather
/// than something the mixer has to rebuild.
/// </remarks>
public interface IDucker
{
    /// <summary>Fast enough to be out of the way before a kick has finished its click.</summary>
    double AttackMs { get; }

    /// <summary>How long the ducked track takes to come back up. Settable while it runs.</summary>
    double ReleaseMs { get; set; }

    /// <summary>Where the follower is: 0 when the key is silent, 1 when it is at full scale.</summary>
    double Level { get; }

    /// <summary>
    /// Takes one frame of the key track and moves the follower towards it. Up quickly, down
    /// slowly, which is what makes a duck breathe rather than chatter.
    /// </summary>
    /// <param name="keyMagnitude">How loud the key track is in this frame, which is read as a magnitude.</param>
    double Next(double keyMagnitude);

    /// <summary>Back to no ducking at all, for a transport stop.</summary>
    void Reset();

    /// <summary>What to multiply the ducked track by, given where the follower is.</summary>
    /// <remarks>
    /// It holds nothing, so the mixer can ask what a depth and a follower come to without a
    /// side chain of its own and a test can ask without an audio device.
    /// </remarks>
    /// <param name="follower">Where the follower stands, from <see cref="Next"/> or <see cref="Level"/>.</param>
    /// <param name="depth">How far down the strip's knob says the track goes at full scale.</param>
    float GainFor(double follower, double depth);
}
