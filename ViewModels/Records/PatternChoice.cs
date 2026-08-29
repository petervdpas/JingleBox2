namespace JingleBox2.ViewModels.Records;

/// <summary>One pattern a slot of the order could be pointed at.</summary>
/// <param name="Index">Where it sits in the song, which is what the order holds.</param>
/// <param name="Name">What it is called, which is what the order list shows.</param>
public sealed record PatternChoice(int Index, string Name)
{
    /// <summary>The name, which is what a list with no template shows.</summary>
    public override string ToString() => Name;
}
