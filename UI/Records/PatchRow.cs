namespace JingleBox2.UI.Records;

/// <summary>One line on the face of a block: which port it belongs to, and which channel of it.</summary>
/// <remarks>
/// **A channel gets a line of its own**, so a stereo port is two dots with two names rather than
/// a pair of dots sharing one. Two dots on one line read as a single fat point at any size that
/// fits on a block, which is the whole thing the shape is there to say: this carries two wires.
///
/// Worked out in one place and handed about, because three things need the same list in the same
/// order: the drawing, the press that lands on a dot, and the cable that has to arrive exactly
/// where the dot was drawn.
/// </remarks>
/// <param name="Port">Which of the side's ports this line belongs to, counting from nought.</param>
/// <param name="Channel">Which channel of that port, counting from nought.</param>
public readonly record struct PatchRow(int Port, int Channel);
