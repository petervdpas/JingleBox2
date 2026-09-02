
namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// What a device's own control offers a hardware knob, which is one answer for both worlds.
/// </summary>
/// <remarks>
/// The rack holds devices. A device is a soundmachine or an effect, and a knob pointed at one
/// names the device's id and the key of the control under the pointer: nothing about the track it
/// happens to be on, nothing about which slot of a chain it is standing in, and nothing about
/// which of the two worlds it came out of. That is what makes a link portable, and it is why the
/// mapping is built in one place rather than written out on each face that offers one.
///
/// Written out three times before this, once on a machine's panel, once on the rack's effect face
/// and once in an effect's own window, and the three had already drifted: the effect's two said
/// <c>Insert</c>, which is the word for a plugin, and every one of those links was thrown away
/// the next time the settings were read.
///
/// The same shape as <c>MixLinks</c> and <c>TransportLinks</c>, which are what a strip and the
/// transport offer, and for the same reason: one maker, so two spellings cannot disagree.
/// </remarks>
public interface ISoundDeviceLinks
{
    /// <summary>
    /// A knob pointed at that parameter of that device.
    /// </summary>
    /// <param name="id">The device's own id, as its manifest carries it.</param>
    /// <param name="named">What it is called, for the words on the front of the link.</param>
    /// <param name="key">The parameter the control turns.</param>
    ControlMapping On(string? id, string? named, string? key);

    /// <summary>
    /// A button pointed at something a device's face can be asked to do.
    /// </summary>
    /// <remarks>
    /// An action rather than a value: taking a recording off a pad is not a number a knob could
    /// be turned to, so it is a press and it is written down as one.
    /// </remarks>
    /// <param name="id">The device's own id.</param>
    /// <param name="named">What it is called.</param>
    /// <param name="action">Which action, in the word the face names it by.</param>
    ControlMapping Action(string? id, string? named, string? action);
}
