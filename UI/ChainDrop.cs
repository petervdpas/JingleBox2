using System.Collections.Generic;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class ChainDrop : IChainDrop
{
    /// <inheritdoc/>
    public int Place(IReadOnlyList<(double Left, double Right)> blocks, double at)
    {
        if (blocks == null || blocks.Count == 0) return 0;

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            if (at > block.Right) continue;

            if (at < block.Left) return i;

            return at < (block.Left + block.Right) / 2 ? i : i + 1;
        }

        return blocks.Count;
    }

    /// <inheritdoc/>
    public int Landing(int moving, int place) => place > moving ? place - 1 : place;
}
