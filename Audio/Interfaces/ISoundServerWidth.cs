namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Tells the sound server how wide this application is, before anything is opened.
/// </summary>
/// <remarks>
/// **Everything here is stereo and the machine was giving us the card's speaker layout instead.**
/// The audio library opens a device with as many channels as the card claims speakers, so that a
/// caller can place a sound on one of them; nothing in this application ever does. On an
/// interface set to a surround profile that came out as four channels going out and six coming
/// in, and what the machine's own patchbay showed was a stream shaped like somebody's speaker
/// arrangement with the two we use at the front of it and the rest carrying nothing. Whether the
/// two we filled were the two anybody was listening to was left to whatever mapped them.
///
/// **The server has a lever for this and it is the plain one.** Its ALSA layer reads a set of
/// properties out of the environment, and among them is the width, which it applies to the
/// hardware parameters the device reports. Told two, the device offers two, and the library has
/// nothing else to choose. Measured on a real machine rather than read and hoped for:
///
/// <code>
/// aplay -D pipewire --dump-hw-params                    CHANNELS: [1 64]
/// PIPEWIRE_ALSA='{ alsa.channels=2 }' aplay ...         CHANNELS: 2
/// </code>
///
/// **Said before anything opens, and once.** The layer reads it when a device is opened, so it
/// has to be standing by then; it costs nothing to set and it reaches the capture as well as the
/// output, since both go through the same layer. A plugin's own process inherits it, which is
/// right: it is a fact about this application rather than about one stream.
///
/// **A machine that has already been told something is left alone.** Somebody who has set this
/// themselves has said something more particular than this knows how to, and overriding it would
/// be this application arguing with its own owner.
/// </remarks>
public interface ISoundServerWidth
{
    /// <summary>Says it, and answers whether this was the one that said it.</summary>
    /// <remarks>
    /// False where there was nothing to say, which is every machine without that server, and
    /// false where somebody had already said something.
    /// </remarks>
    bool Ask();

    /// <summary>What was arranged, in words fit for a log.</summary>
    string Said();
}
