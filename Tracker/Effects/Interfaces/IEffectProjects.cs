namespace JingleBox2.Tracker.Effects.Interfaces;

/// <summary>
/// The effects this installation has, held for the run.
/// </summary>
/// <remarks>
/// Every member of it is the rack's, since an effect asks nothing here a machine does not: what
/// is there, is this id one of them, which one is it, and what does its face look like. A name
/// for <see cref="Tracker.Interfaces.IRackDevices{T}"/> of effects, so a caller says what it
/// wants rather than a shape with a parameter in it.
/// </remarks>
public interface IEffectProjects : Tracker.Interfaces.IRackDevices<EffectProject>
{
}
