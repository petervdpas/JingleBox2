namespace JingleBox2.Rack.Effects.Interfaces;

/// <summary>
/// What an effect is, as far as anything outside it is concerned.
/// </summary>
/// <remarks>
/// The parallel of what a machine says about itself, and deliberately the same four answers: the
/// host has to be able to list what effects there are, tell them apart, put one on the rack and
/// paint it. What it is made of, what it does to a block of audio and the panel it is edited on
/// are separate contracts, so an effect can be described without any of them being loaded.
///
/// An effect is not a machine. It is sent no notes, it has no keyboard, no zones, no pads and no
/// kit, and what it is doing is happening to a whole track rather than to a voice. What the two
/// do share is the drawing, which is why the panel contracts sit in
/// <c>JingleBox2.Rack.Faces</c> and are named for neither world.
/// </remarks>
public interface IEffect
{
    /// <summary>
    /// The name this effect is known by in files, forever.
    /// </summary>
    /// <remarks>
    /// Written into every song that has one on a chain, so it can never change: an effect that
    /// renames itself silences every chain anybody built with it. It is also what decides
    /// whether the effect can be had at all, since the id is what says which engine is behind
    /// it and an id this build has no engine for is read off disc and left there.
    /// </remarks>
    string Id { get; }

    /// <summary>What it is called on the rack.</summary>
    string Name { get; }

    /// <summary>The one line under the name saying what it does.</summary>
    string Summary { get; }

    /// <summary>The colour it is, which every other shade of it is made from.</summary>
    string Colour { get; }
}
