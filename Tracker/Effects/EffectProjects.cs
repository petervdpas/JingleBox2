using JingleBox2.Tracker.Effects.Interfaces;

namespace JingleBox2.Tracker.Effects;

/// <inheritdoc/>
/// <remarks>
/// Nothing of its own: what effects this installation has is the rack's own question, asked the
/// same way for both worlds. The interface stays as a name for it, since everything that takes
/// one names what it wants rather than a shape with a parameter in it.
/// </remarks>
public sealed class EffectProjects : RackDevices<EffectProject>, IEffectProjects
{
}
