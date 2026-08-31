using System.Collections.Generic;

namespace JingleBox2.Midi.Records;

/// <summary>
/// What came back off a template: the links it described, and how much of it did not read.
/// </summary>
/// <remarks>
/// Both halves, because either on its own is a lie. The links alone would have an import from a
/// newer version look complete while half of it was silently dropped; the count alone would
/// throw away the part that works. A template mostly readable is mostly worth having, and the
/// person is told the rest.
/// </remarks>
/// <param name="Links">Everything that could be read, ready to be laid down.</param>
/// <param name="Skipped">How many lines described something this build has no word for.</param>
/// <param name="Controller">The port these were pointed at, or the name the file carried.</param>
/// <param name="Found">Whether that controller is one this computer can actually see.</param>
public sealed record ControlTemplateReading(
    IReadOnlyList<ControlMapping> Links,
    int Skipped,
    string Controller,
    bool Found);
