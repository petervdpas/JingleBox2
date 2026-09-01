namespace JingleBox2.Tracker.Machines.Records;

/// <summary>One machine a song plays on that this installation is not offering.</summary>
/// <remarks>
/// One record and one list for both ways a machine can be absent, because to everything that
/// decides whether an instrument can play they are the same fact: the rack decides which
/// machines a song can be given, so a machine taken off it is one this installation is not
/// offering, and an instrument on it is in exactly the position of one whose machine was never
/// registered. Silent, no panel, grey, and named on the way in.
///
/// <see cref="Registered"/> is the only place the difference shows, and it shows there because
/// the two have different remedies: a machine that is not registered wants a zip imported, and
/// one that is off the rack wants a press of the picker under it. Asked twice, in two lists, by
/// every caller, they would have to be kept in step for ever; asked once with the reason on the
/// answer, a caller that does not care about the difference never learns there is one.
/// </remarks>
/// <param name="Id">The slot the song's instruments name.</param>
/// <param name="Name">What the machine is called, which the song remembers on its own.</param>
/// <param name="Ships">True when the program has a copy to add. False for one that came in from a zip.</param>
/// <param name="Registered">
/// True when this installation has the machine and it is off the rack, false when it has not got
/// the machine at all.
/// </param>
public sealed record MissingMachine(string Id, string Name, bool Ships, bool Registered = false)
{
    /// <summary>Why it is not being offered, as the half sentence that follows the name.</summary>
    /// <remarks>
    /// Here rather than in the two dialogs that print it, for the reason
    /// <see cref="Tracker.TrackerInstrument.Detail"/> is on the instrument rather than on the
    /// two lists that show it: written out twice, the two eventually disagree, and the way that
    /// fails is one page calling a machine absent while the other calls it shelved.
    /// </remarks>
    public string Because => Registered ? "is not on the rack" : "is not registered";

    /// <summary>The same, as the label that heads it in a list of several.</summary>
    public string Label => Registered ? "Not on the rack" : "Not registered";
}
