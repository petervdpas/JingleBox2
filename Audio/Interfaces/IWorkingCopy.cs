namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The copy of a take that the editor works on, and the two ways it ends.
/// </summary>
/// <remarks>
/// **Nothing the editor does touches the take on the shelf until somebody says Save.** Every
/// tool rewrites this copy instead, which is what makes an explicit save mean anything and what
/// makes closing the window without saving leave the take exactly as it was found.
///
/// It is also half of the undo. A step is the edit rather than the audio, so standing further
/// back is a fresh copy of the take with the steps before that point done again: the take on the
/// shelf is the one thing that is certainly untouched, which makes it the only honest place to
/// replay from.
///
/// A file rather than an array in memory, and the reason is the rest of the application: the
/// picture is read from a path, the preview is played from a path, and every edit is written
/// through a path already. Held in memory it would be a second way of doing all three, which is
/// the fault this codebase keeps paying for.
/// </remarks>
public interface IWorkingCopy
{
    /// <summary>Where the copy is, or nothing when no take is open.</summary>
    string? Path { get; }

    /// <summary>True while there is a copy to work on.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Takes a copy of a take and answers where it is.
    /// </summary>
    /// <remarks>
    /// Whatever was open before is let go of, so opening one take after another leaves one file
    /// behind rather than a folder of them.
    /// </remarks>
    /// <param name="takePath">The take on the shelf.</param>
    /// <returns>The copy's path, or nothing where there is no such take.</returns>
    string? Open(string takePath);

    /// <summary>Throws the working copy away and takes the take again, which is where undo lands.</summary>
    /// <returns>True where the copy is back to what the shelf holds.</returns>
    bool Fresh();

    /// <summary>
    /// Puts the working copy over the take, all of it or none of it.
    /// </summary>
    /// <remarks>
    /// Through a file beside the real one and a move, like everything else written here, because
    /// the one thing that may not happen is a take left half rewritten: that is somebody's only
    /// copy of a performance. The working copy stays where it is afterwards, so the window is
    /// still open on something.
    /// </remarks>
    /// <returns>True where the take was replaced.</returns>
    bool Keep();

    /// <summary>Lets the copy go and deletes it. Safe with nothing open.</summary>
    void Close();
}
