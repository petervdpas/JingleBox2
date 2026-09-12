using System.Collections.Generic;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Where the links are kept: what every controller is pointed at, on this installation.
/// </summary>
/// <remarks>
/// **A file of its own and not a corner of the settings.** It was in <c>config.json</c>, which is
/// the document somebody's preferences are written from and is serialised whole every time
/// anything on any page moves. The links are not a preference: they are written from the MIDI
/// thread as a hand learns a knob, they are a fifth of that file on an ordinary installation, and
/// they are the one thing in it a person carries between machines.
///
/// **The links are what is stored and the templates are what is read**, which is the one thing to
/// know about this. A template is the travelling form: it names the controller as its profile
/// calls it and never a port, deliberately, because the port is the part that does not travel. So
/// a template is what a face offers and what an export writes, and storing it would mean settling
/// a port again on every start. What is kept here is the links as they stand, port and all.
///
/// A file that will not read is no links rather than a start that fails. What is lost is a
/// layout, which is a morning's work; what would be lost the other way is the application, since
/// this is read before there is a window to report anything in.
/// </remarks>
public interface IRemoteControlLinks
{
    /// <summary>Where the file is.</summary>
    string Path();

    /// <summary>
    /// The links as the file holds them, without touching a disc.
    /// </summary>
    /// <remarks>
    /// Apart from the writing for the reason <c>IConfigStore.Written</c> is: a writer that has to
    /// be right rather than merely quick compares what it would write with what it wrote, and
    /// that comparison costs nothing only where working out the text is not a write.
    /// </remarks>
    /// <param name="links">Everything pointed at anything.</param>
    string Written(IEnumerable<ControlMapping>? links);

    /// <summary>Puts that text on the disc, whole or not at all.</summary>
    /// <param name="written">What <see cref="Written"/> answered.</param>
    void Keep(string written);

    /// <summary>What the last run left, or nothing where there has not been one.</summary>
    IReadOnlyList<ControlMapping> Read();
}
