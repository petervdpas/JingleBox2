namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Whether an output can actually be opened, asked by opening it.
/// </summary>
/// <remarks>
/// **A device the system lists is not a device this application can have.** The list comes from
/// the audio library and says what the machine has, which is not the same question as what is
/// free: on a machine with a sound server, the server holds the card it is playing through and
/// nothing else may open it. Picking one of those got as far as the attempt and came back as
/// <c>Bass.Init failed: Busy</c>, drawn as a stack trace across the settings page, over a choice
/// the page had offered.
///
/// So the choice is not offered. Asked by opening the device and letting it go again, because
/// there is no other way to know: the library's own list says enabled and default and says
/// nothing about who has it, and the reason it is busy belongs to the system rather than to
/// anything here.
///
/// **The device this application already holds is never probed**, since opening it again would
/// mean taking the sound down to ask a question whose answer is obviously yes.
///
/// Behind a contract because opening a sound card is the one thing a test cannot do, and because
/// what counts as open differs by nothing here yet and will the day another library is used.
/// </remarks>
public interface IOutputProbe
{
    /// <summary>Whether that device opens, and is left exactly as it was found.</summary>
    /// <remarks>
    /// True where it opened and was let go, and true where the library says it is already ours.
    /// False for everything else, including anything thrown: a device that cannot be asked about
    /// is one that cannot be promised, and offering it is what this exists to stop.
    /// </remarks>
    /// <param name="device">The library's own index for the device.</param>
    bool Opens(int device);
}
