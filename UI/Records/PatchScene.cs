using System.Collections.Generic;

namespace JingleBox2.UI.Records;

/// <summary>Everything the patchbay draws: the blocks, and the cables between them.</summary>
/// <remarks>
/// The two together rather than one at a time, because they are read from the same reading of
/// the machine and a cable naming a block that is not in the list is a cable to nowhere. Handed
/// over whole and swapped whole, which is the rule this codebase already keeps for anything two
/// threads or two properties could otherwise see half of.
/// </remarks>
/// <param name="Nodes">The blocks, in the order they should first appear.</param>
/// <param name="Links">The cables, each naming ports that are on those blocks.</param>
public sealed record PatchScene(
    IReadOnlyList<PatchNode> Nodes,
    IReadOnlyList<PatchLink> Links);
