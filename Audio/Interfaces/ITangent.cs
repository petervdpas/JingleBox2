namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The hyperbolic tangent, which is the curve everything in this application bends with.
/// </summary>
/// <remarks>
/// One function is a thin thing to put an interface over, and it earns one because there are two
/// honest ways to work it out and which of them runs is a setting rather than a fact. The drive
/// on a machine, the drive inside an effect and the master's own soft clip are all this curve, so
/// a decision taken here is taken in every one of them at once.
///
/// **It is worth the trouble because of where it sits.** Measured at the mixer's own ceiling of
/// forty eight voices, with the optimiser on, a saw through a resonant filter costs 7.9% of each
/// block's own time and the drive is 4.5 of that: more than half the voice, and twenty times what
/// the filter costs. The reason is that the tangent is a call into the system's maths library and
/// stays a call however the rest of the loop is compiled, so everything around it got five times
/// cheaper when the optimiser was turned on and it did not.
///
/// What an implementation may not do is disagree at the ends or in the middle. Nought is nought,
/// the curve is odd, it rises everywhere, it reaches one and never passes it, and something that
/// is not a number comes back not a number rather than as an index into somebody's array.
/// </remarks>
public interface ITangent
{
    /// <summary>The curve at a point.</summary>
    /// <param name="x">How hard the signal is being pushed into it.</param>
    /// <returns>Between -1 and 1, or not a number where that is what was handed in.</returns>
    double Of(double x);
}
