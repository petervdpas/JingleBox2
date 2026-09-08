using System.Collections.Generic;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// Which of this application's own sources are patched into the recorder's input.
/// </summary>
/// <remarks>
/// **This is an arrangement rather than a fact about the engine, which is why it is kept at
/// all.** The rest of the picture is read off what the machine is doing and off what the code
/// already does: the pads reach the desk because that is what a desk is. These cables are the
/// one part somebody decides, so they have to still be there tomorrow.
///
/// Kept in the settings and not in a song, for the reason everything else on this page is: the
/// patchbay is about this installation. A song carried to another machine has no business
/// arriving with the pads wired into somebody's recorder.
///
/// A block by its id and nothing else. Which channel goes where is <see cref="UI.Interfaces.IPatchWiring"/>,
/// and what it means once the audio moves is the recorder's; all this says is that a cable is
/// there.
/// </remarks>
public interface IPatchedIn
{
    /// <summary>The blocks whose output is patched into the input, by id.</summary>
    /// <remarks>
    /// Empty on a fresh installation, which is what nothing patched looks like and is what the
    /// application has always done.
    /// </remarks>
    IReadOnlyList<string> Sources { get; }

    /// <summary>Whether that block is patched in.</summary>
    /// <param name="node">The block's id.</param>
    bool Holds(string node);

    /// <summary>Patches one in, and does nothing where it already is.</summary>
    /// <remarks>
    /// Twice is once, since a cable drawn again over one that is already there is the same cable
    /// and not a second one: two entries would mean the source arriving into the input twice and
    /// therefore twice as loud.
    /// </remarks>
    /// <param name="node">The block's id.</param>
    void Add(string node);

    /// <summary>Takes one out, and does nothing where it was not in.</summary>
    /// <param name="node">The block's id.</param>
    void Remove(string node);
}
