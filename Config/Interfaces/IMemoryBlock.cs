using System;

namespace JingleBox2.Config.Interfaces;

/// <summary>
/// One block of what this application knows, and the two things it says about itself.
/// </summary>
/// <remarks>
/// **The unit is the block rather than the field, and that is what makes it safe.** Whether
/// something is written to disc is asked of the block, so a writer that walks the blocks never
/// sees one it must not write. Written as a mark on a field inside a document that is written
/// whole, the one rule that matters would be guarded by an attribute anybody can forget.
///
/// A block is deliberately not the document. A document is a shape somebody serialises: adding a
/// name and a flag to <see cref="AppConfig"/> would put both of them in everybody's settings file
/// the next time it was written. So the block holds the document and answers for it.
///
/// See <c>docs/memory-blocks.md</c> for where this is going: the interface writes a block, and
/// the writers, the routing and the views observe it.
/// </remarks>
public interface IMemoryBlock
{
    /// <summary>What the block is called, for a writer and for a log line.</summary>
    string Name { get; }

    /// <summary>
    /// Whether this survives the run.
    /// </summary>
    /// <remarks>
    /// **False is the interesting answer.** What the input is pointed at and whether it is being
    /// heard are true of this session and must never be written down: an application that started
    /// with somebody's browser already unplugged from its own speakers, or with a microphone
    /// already open into the mix, would be doing something nobody had asked for that morning.
    /// </remarks>
    bool Kept { get; }

    /// <summary>
    /// Said when something in the block has moved.
    /// </summary>
    /// <remarks>
    /// **A hint and not the whole truth**, which is the one thing to know about it. A block is a
    /// document with fields and lists in it, and a list added to in place moves nothing anybody
    /// could have subscribed to, so a writer that only ever heard this would miss whatever nobody
    /// remembered to say. It is what lets a writer be quick rather than what lets it be right:
    /// what makes it right is comparing what it would write with what it wrote, which costs
    /// nothing worth counting on a clock slow enough to be a net.
    ///
    /// So nothing here is answerable for saying this at every moment it could be said, and
    /// nothing downstream may assume it was.
    /// </remarks>
    event Action? Changed;
}
