namespace JingleBox2.Diagnostics.Interfaces;

/// <summary>
/// The log as a file on disc: appending to it, and starting a new one when it gets big.
/// </summary>
/// <remarks>
/// Everything here fails in silence. A log is not worth an exception in the thing it is a log
/// of, and the run where the disc is full or the folder is read-only is exactly the run
/// somebody is trying to get to the bottom of.
/// </remarks>
public interface ILogFile
{
    /// <summary>Adds to the end of the file, making the folder around it if it is not there.</summary>
    /// <param name="path">The file, which need not exist yet.</param>
    /// <param name="text">What to add, already formed into lines.</param>
    /// <returns>Whether it went in. False is a disc that would not take it, not an empty batch.</returns>
    bool Append(string path, string text);

    /// <summary>
    /// Keeps one old file and starts a new one when the current one gets big.
    /// </summary>
    /// <remarks>
    /// Two files of a few megabytes is a bounded cost for something somebody may leave switched
    /// on for a week, which is the case this exists for. The old one keeps the same name with
    /// <c>.old</c> on the end, so there is never more than one of them.
    /// </remarks>
    /// <param name="path">The file that may have got too big.</param>
    /// <param name="mostBytes">How big it is allowed to get before it is rolled over.</param>
    /// <returns>Whether it was rolled over just now.</returns>
    bool Roll(string path, long mostBytes);
}
