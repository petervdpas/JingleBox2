namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Keeps the templates block up with what is pointed at what.
/// </summary>
/// <remarks>
/// **The write path, in one place.** A hand points a control at something and what that comes to
/// is a link; what the application reads is templates; and this is the one thing that turns the
/// first into the second. Without it the block would be a snapshot taken at startup and quietly
/// one behind the hardware from the first knob somebody learned.
///
/// It is a seam rather than two lines inside the window, because what it does can be put a
/// question to without a window, a controller or a settings file: given links, the block holds
/// the templates they come to, and it says so exactly once per act.
///
/// One direction throughout. Nothing here reads the block, and nothing that reads the block
/// reaches back through this.
/// </remarks>
public interface IControlTemplatesFromLinks
{
    /// <summary>
    /// Reads the links into the block as they stand.
    /// </summary>
    /// <remarks>
    /// The whole list rather than the one template that moved, which is what it can afford: a cut
    /// of fifteen links is microseconds and of five hundred is under a millisecond, against the
    /// block holding something one behind what the hardware is really doing.
    /// </remarks>
    /// <param name="said">
    /// Whether to say the block moved. False for the first fill, which is what was already on
    /// disc: a writer hearing a hint before anybody has touched anything would put the file back
    /// at startup for nothing.
    /// </param>
    void Fill(bool said);
}
