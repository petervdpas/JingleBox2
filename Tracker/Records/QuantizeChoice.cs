namespace JingleBox2.Tracker.Records;

/// <summary>One entry of the quantize menu: a musical value, and the lines it works out at.</summary>
/// <param name="Label">
/// What it is called. The value first, since that is what somebody is choosing, and the lines
/// after it in brackets, since that is what the pattern will actually do.
/// </param>
/// <param name="Lines">How many lines the notes are pulled onto.</param>
public sealed record QuantizeChoice(string Label, int Lines)
{
    /// <summary>The label, which is what a list with no template shows.</summary>
    public override string ToString() => Label;
}
