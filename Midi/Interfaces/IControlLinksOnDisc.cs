namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// The links file following the links block, with nobody deciding when.
/// </summary>
/// <remarks>
/// **Told and also looking**, the two halves of every deferred write here. The hint makes the
/// file keep up with a hand; comparing what it would write with what it wrote makes it right, and
/// the second half is not a belt here. A link learned says so, and so does one taken off, but the
/// router deciding what kind of control is sending writes a pickup onto a mapping from the MIDI
/// thread with nobody having asked for anything.
///
/// It observes the block and nothing else. What moved a link, and which page was in front when it
/// did, is not this one's business.
/// </remarks>
public interface IControlLinksOnDisc
{
    /// <summary>Looks once, and writes where what it would write differs from what it wrote.</summary>
    /// <returns>Whether it wrote.</returns>
    bool Check();
}
