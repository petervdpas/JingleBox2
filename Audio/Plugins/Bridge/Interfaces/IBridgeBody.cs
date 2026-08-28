using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins.Bridge.Interfaces;

/// <summary>
/// What a message carries, written down and read back.
/// </summary>
/// <remarks>
/// The body of every message the two processes send each other, in pairs: one that writes and
/// one that reads. They are pairs on purpose, and on one interface on purpose, because the two
/// halves of a pair are the one thing here that can disagree silently. A writer that gains a
/// field and a reader that does not gives every message after it the wrong shape, and there is
/// no error anywhere: a plugin simply comes back with the wrong parameter values, or a name
/// made of somebody else's bytes.
///
/// Everything reads defensively, and that is a requirement rather than a courtesy. A payload
/// shorter than the shape it is being read as answers with what was whole rather than throwing,
/// and a count read off the wire is never used to size an array. The other end of this is a
/// process that may have died in the middle of a write, so a reader that threw, or that
/// allocated whatever a damaged four bytes asked for, would turn a plugin crash into an
/// application crash, which is the one thing this whole arrangement exists to prevent. Both
/// were true of the two longest messages here until a test asked.
/// </remarks>
public interface IBridgeBody
{
    /// <summary>A list of strings: the count, then each one in turn.</summary>
    /// <remarks>
    /// Used for everything wordy the bridge carries, which is a plugin's greeting, a value read
    /// as the plugin words it, and the reason something failed. A null string goes down as an
    /// empty one, so the count on the wire always matches what follows it.
    /// </remarks>
    byte[] Words(params string[] words);

    /// <summary>Reads back what <see cref="Words"/> wrote.</summary>
    string[] ReadWords(byte[] payload);

    /// <summary>
    /// A parameter's id and a value: twelve bytes, the id first. Most of the traffic on this
    /// wire is one of these.
    /// </summary>
    byte[] Number(uint id, double value);

    /// <summary>
    /// Reads back what <see cref="Number"/> wrote. A payload too short to hold one reads as
    /// nought rather than throwing: a message that arrived damaged should cost the message.
    /// </summary>
        (uint Id, double Value) ReadNumber(byte[] payload);

    /// <summary>One number on its own, which is what a value asked for comes back as.</summary>
    byte[] Double(double value);

    /// <summary>Reads back what <see cref="Double"/> wrote, or nought from a short payload.</summary>
    double ReadDouble(byte[] payload);

    /// <summary>
    /// Two numbers, which on this wire is always a width and a height: a window's size going
    /// out, and the size the plugin settled on coming back.
    /// </summary>
    byte[] Pair(int first, int second);

    /// <summary>Reads back what <see cref="Pair"/> wrote, or a pair of noughts from a short payload.</summary>
        (int First, int Second) ReadPair(byte[] payload);

    /// <summary>
    /// Three numbers: a window's width, its height, and whether the plugin will follow it being
    /// dragged bigger. The answer to opening a plugin's own interface.
    /// </summary>
    byte[] Three(int first, int second, int third);

    /// <summary>Reads back what <see cref="Three"/> wrote, or three noughts from a short payload.</summary>
        (int First, int Second, int Third) ReadThree(byte[] payload);

    /// <summary>
    /// A window handle, always as eight bytes whatever the machine's own pointer is, so the two
    /// halves cannot disagree about how long a handle is.
    /// </summary>
    byte[] Handle(nint window);

    /// <summary>Reads back what <see cref="Handle"/> wrote. Nought means no window.</summary>
    nint ReadHandle(byte[] payload);

    /// <summary>Every parameter the plugin has, in the order it lists them.</summary>
    byte[] Parameters(IReadOnlyList<PluginParameter> parameters);

    /// <summary>
    /// Reads back what <see cref="Parameters"/> wrote, field for field in the same order.
    /// </summary>
    /// <remarks>
    /// This and its other half are the one place on this wire where a field added to one side
    /// and not the other would be read as nonsense rather than as a message that is too short:
    /// every parameter after the changed one would be read from the wrong place. They are kept
    /// next to each other for that reason.
    /// </remarks>
    PluginParameter[] ReadParameters(byte[] payload);
}
