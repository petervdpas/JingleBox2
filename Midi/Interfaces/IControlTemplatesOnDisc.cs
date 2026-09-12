namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// The templates file following the templates block, with nobody deciding when.
/// </summary>
/// <remarks>
/// **Told and also looking, which are the two halves of one job.** A hint is what makes the file
/// keep up with a hand; comparing what it would write with what it wrote is what makes it right.
/// Neither half is enough on its own here, and the reason is in what moves a template: a link
/// learned says so, and so does one taken off, but the router deciding what kind of control is
/// sending changes a template's pickup from the MIDI thread with nobody having asked for
/// anything. A writer that only ever heard would miss that; one that only ever looked would be
/// a clock nobody set.
///
/// It observes the block and nothing else. What moved a template, and which page was in front
/// when it did, is not this one's business: the block says it moved, and the file catches up.
///
/// <see cref="IControlTemplates.Folder"/> is deliberately untouched by any of this. That folder
/// is what somebody owns, one file per template, exported or given or written by hand; this
/// writes the one file that says what the desk is doing now.
/// </remarks>
public interface IControlTemplatesOnDisc
{
    /// <summary>
    /// Looks once, and writes where what it would write differs from what it wrote.
    /// </summary>
    /// <remarks>
    /// Public so it can be asked rather than waited for, which is what lets a five second window
    /// be tested in no time at all.
    /// </remarks>
    /// <returns>Whether it wrote.</returns>
    bool Check();
}
