namespace JingleBox2.UI.Records;

/// <summary>What a block is putting out, for the meter beside its details.</summary>
/// <remarks>
/// Whether there is an answer at all is part of the answer. A block on the machine is somebody
/// else's program and this application measures nothing about it, so a meter there would be a
/// bar sitting at nought and reading as silence rather than as a question nobody can answer.
/// </remarks>
/// <param name="Known">Whether this application can say anything about this block.</param>
/// <param name="Left">The left side's peak, nought to one.</param>
/// <param name="Right">The right side's.</param>
public readonly record struct PatchLevel(bool Known, float Left, float Right);
