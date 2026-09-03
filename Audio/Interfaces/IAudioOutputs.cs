using JingleBox2.Audio.Enums;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// One number for an output, out of two lists that both start at nought.
/// </summary>
/// <remarks>
/// The system's devices are numbered from nought and so are the ASIO drivers, so a bare number
/// says nothing about which of the two it is. What is stored in the settings is one number, and
/// making it two fields would be two things that have to agree, which is how the same fault gets
/// written down twice and then diverges.
///
/// So the number carries both: the system's keep the numbers they always had, which is what makes
/// every settings file written before ASIO existed go on meaning what it meant, and ASIO's are
/// lifted clear of them. One place composes and one place takes apart, and both are here.
/// </remarks>
public interface IAudioOutputs
{
    /// <summary>Where the ASIO drivers start, so the two lists cannot collide.</summary>
    /// <remarks>
    /// High enough that no machine will ever have that many system endpoints, and round enough
    /// that a number in a log or a settings file can be read by eye: 1000 and up is ASIO.
    /// </remarks>
    int AsioFrom { get; }

    /// <summary>The one number for that device in that world.</summary>
    /// <param name="kind">Which world it is in.</param>
    /// <param name="index">Its own number inside that world, counting from nought.</param>
    int Numbered(AudioOutputKind kind, int index);

    /// <summary>Which world a stored number is in, and which device inside it.</summary>
    /// <remarks>
    /// A number below nought is nobody's: it is what the settings hold before anything has been
    /// picked, and it comes back as the system's own default rather than as an error.
    /// </remarks>
    /// <param name="number">The number as it was stored.</param>
    (AudioOutputKind Kind, int Index) Which(int number);
}
