namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The form Windows wants an endpoint written in when it is told where a program should play.
/// </summary>
/// <remarks>
/// **The system names one endpoint two ways and only accepts one of them here.** What the
/// enumerator hands back is a plain id; what the policy call takes is that id inside a device
/// interface path, with a prefix and a class guid around it. Handing over the plain one is
/// accepted and does nothing, which is the worst kind of wrong: no error, no sound moved, and
/// nothing anywhere to say why.
///
/// A rule of its own because it is string work that can only be got right or silently wrong, and
/// because it can be put a question to on a machine that has no such call.
/// </remarks>
public interface IMmDeviceToken
{
    /// <summary>Wraps a plain endpoint id in the form the policy call takes.</summary>
    /// <param name="endpoint">The id as the enumerator gave it.</param>
    string Wrap(string endpoint);

    /// <summary>And takes it back out, for reading what the system says is set.</summary>
    /// <remarks>
    /// Anything that is not in that form is handed back as it is, since the system answers with
    /// an empty string where a program has no output of its own.
    /// </remarks>
    /// <param name="token">What the system said.</param>
    string Unwrap(string token);
}
