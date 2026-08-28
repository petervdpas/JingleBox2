using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Tracker.Machines.Records;

/// <summary>One machine a song plays on that this installation has not got.</summary>
/// <param name="Id">The slot the song's instruments name.</param>
/// <param name="Name">What the machine is called, which the song remembers on its own.</param>
/// <param name="Ships">True when the program has a copy to add. False for one that came in from a zip.</param>
public sealed record MissingMachine(string Id, string Name, bool Ships);
