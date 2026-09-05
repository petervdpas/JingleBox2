using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// The system's own, which is what every sample of this application went through before there was
/// anything else, and is therefore what "off" has to mean: a switch whose off position is not
/// exactly what happened before is a switch nobody can use to decide anything.
/// </remarks>
public sealed class Tangent : ITangent
{
    /// <inheritdoc/>
    public double Of(double x) => Math.Tanh(x);
}
