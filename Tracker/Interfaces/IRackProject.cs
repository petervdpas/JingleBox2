using JingleBox2.Rack.Faces.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// A box on the rack as its folder holds it: a machine or an effect, before anything asks which.
/// </summary>
/// <remarks>
/// The two worlds are separate everywhere it matters, and identical in the four things a folder
/// of them has to answer to be read at all: what it calls itself in files, what it is called on
/// the rack, the sentence under that, the colours it is painted in, and where it was read from.
/// Nothing about notes or audio is here, which is the test of whether something belongs: a
/// question this cannot answer is a question about one world rather than about the rack.
///
/// It exists so the rules for a folder of boxes can be written once. Which folders ship, which
/// are this installation's, what has been offered, and what is brought up to date against what
/// are facts about a rack rather than about a machine, and they were written for machines first
/// because machines came first.
/// </remarks>
public interface IRackProject
{
    /// <summary>What it is called in files, forever, and what decides whether it has an engine.</summary>
    string Id { get; }

    /// <summary>What it is called on the rack, which is its own to change.</summary>
    string Name { get; }

    /// <summary>The one line under the name saying what sort of thing it is.</summary>
    string Summary { get; }

    /// <summary>Who made it, for one that is going to be handed to somebody else.</summary>
    string Author { get; }

    /// <summary>Bumped by whoever makes it, and shown beside the name wherever it is listed.</summary>
    string Version { get; }

    /// <summary>Its colours, which are its own and not the application's.</summary>
    PanelTheme Theme { get; }

    /// <summary>The folder it was read from, or empty for one that has never been saved.</summary>
    string Folder { get; }

    /// <summary>Whether it has a folder yet, which is what everything touching the disc holds against.</summary>
    /// <remarks>
    /// Asked rather than worked out from <see cref="Folder"/> at each call site, since one of
    /// those would eventually test the wrong thing about an empty string.
    /// </remarks>
    bool IsSaved { get; }
}
