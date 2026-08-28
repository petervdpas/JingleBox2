using System;

namespace JingleBox2.Audio;


/// <summary>
/// The loudest sample seen on each side over one reading, 0 for silence and 1 for full scale.
/// </summary>
/// <remarks>
/// A peak rather than an average, because a meter is watched for the moment a signal touches the
/// top and an average would hide exactly that. It says nothing about how long ago it was taken:
/// holding a reading and letting it fall is the business of whatever draws it.
/// </remarks>
/// <param name="Left">The left side, or the only side a mono signal has.</param>
/// <param name="Right">The right side, which for a mono signal repeats the left.</param>
public readonly record struct StereoLevel(float Left, float Right)
{
    /// <summary>Nothing sounding, which is what every reading of a stopped channel is.</summary>
    public static readonly StereoLevel Silent = new(0, 0);

    /// <summary>The louder of the two, for anything that shows a single bar.</summary>
    public float Peak => Math.Max(Left, Right);
}
