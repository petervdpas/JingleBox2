namespace JingleBox2.Diagnostics.Records;

/// <summary>
/// How busy the processors are and how full the memory is, and nothing else, for the footer.
/// </summary>
/// <param name="Cpu">How busy all the processors are together, nought to one.</param>
/// <param name="Memory">How much of the memory is in use, nought to one.</param>
public readonly record struct SystemGlance(double Cpu, double Memory);
