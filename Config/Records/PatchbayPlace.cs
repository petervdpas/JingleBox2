namespace JingleBox2.Config.Records;

/// <summary>Where one patchbay block was left, so it is there again tomorrow.</summary>
/// <remarks>
/// Written down by the block's own address rather than by its position in a list, since what is
/// on the patchbay changes between one run and the next: a program that was playing yesterday is
/// not there this morning, and a place remembered by row number would land on whatever took its
/// row.
///
/// A block nobody has moved is not in the file at all. What is stored is the arrangement
/// somebody made, and everything else falls where the graph puts it, so a patchbay that has
/// never been touched has nothing to read back and a block added to this application later opens
/// where it was meant to rather than at the top left.
/// </remarks>
public sealed class PatchbayPlace
{
    /// <summary>Which block, by the id its ports name it with.</summary>
    public string Node { get; set; } = "";

    /// <summary>How far across the surface it was left.</summary>
    public double X { get; set; }

    /// <summary>How far down.</summary>
    public double Y { get; set; }
}
