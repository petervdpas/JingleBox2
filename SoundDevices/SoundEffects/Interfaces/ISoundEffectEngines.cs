namespace JingleBox2.SoundDevices.SoundEffects.Interfaces;

/// <summary>
/// Which effects this build can actually make, and the making of one.
/// </summary>
/// <remarks>
/// An effect is a face over an engine, and the engine is compiled into the application: the
/// folder on disc carries the face, the parameters and the presets, and nothing that makes a
/// sound. So an id is either one this build knows how to build or it is not, and one it does not
/// know is read off disc and left there rather than put on the rack as a box that cannot sound.
///
/// A question rather than a switch statement in the registry, for two reasons. A test can hand
/// one over that knows an id nobody has written an engine for, which is the only way the folder
/// rules can be put a question to before there are any engines at all. And an engine added later
/// is a line here beside the class that does the work, rather than an edit in a registry that
/// has no business knowing what a delay is.
///
/// Deliberately not an enum of engines with numbers in it. A machine's engine is
/// <c>TrackerInstrumentKind</c> and its numbers are in every song ever saved, because a song
/// says what an instrument is on; a chain writes down the effect's id and never its engine, so
/// nothing here is ever written to a file and there is no number to keep still.
/// </remarks>
public interface ISoundEffectEngines
{
    /// <summary>True when this build has an engine for that id.</summary>
    /// <remarks>
    /// The id is resolved to an engine first, the same way <see cref="Make"/> resolves it, so
    /// this answers for an effect somebody made in the designer as readily as for one that ships.
    /// </remarks>
    /// <param name="id">The id off an effect's manifest.</param>
    bool Has(string? id);

    /// <summary>True when this build can make that engine.</summary>
    /// <remarks>
    /// Asked by the registry, which is holding a manifest that is not registered yet and so
    /// cannot be looked up by id. An engine this build has not got is what makes an effects
    /// folder from a later version harmless.
    /// </remarks>
    /// <param name="engine">The engine a manifest names.</param>
    bool HasEngine(string? engine);

    /// <summary>
    /// Which engine an effect plays: the one it names, or the one its id used to imply.
    /// </summary>
    /// <remarks>
    /// An effect is a face over an engine, and which engine was worked out from the effect's own
    /// id for as long as there were three effects and their ids were written into the
    /// application. An effect is made in the designer and named by whoever made it, so the
    /// manifest says which engine it wants and the id is the effect's own.
    ///
    /// The three that shipped before that name no engine, and every chain on anybody's disc
    /// writes their ids down, so those three ids are still understood. Nothing else is.
    /// </remarks>
    /// <param name="id">The effect's own id, which a chain writes down.</param>
    /// <param name="named">What its manifest says, which is usually nothing for the first three.</param>
    string? EngineOf(string? id, string? named);

    /// <summary>
    /// One of that effect, ready to be put on a chain, or nothing when this build has no engine
    /// for the id.
    /// </summary>
    /// <remarks>
    /// A fresh one each time. Two of the same effect on one track are two objects with their own
    /// delay lines and their own filter state, the way two of the same pedal are two pedals.
    /// </remarks>
    /// <param name="id">The id off an effect's manifest.</param>
    /// <param name="sampleRate">What the mix is running at, since a time is in seconds.</param>
    /// <param name="maxFrames">
    /// The longest block it will ever be handed. An effect of ours sizes itself from the rate
    /// rather than from the block, since nothing here works a block at a time, but the question
    /// is asked because a thing that goes on a chain may one day need it.
    /// </param>
    ISoundEffectEngine? Make(string? id, int sampleRate, int maxFrames);
}
