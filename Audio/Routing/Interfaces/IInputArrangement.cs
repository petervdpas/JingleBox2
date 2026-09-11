using System;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing.Interfaces;

/// <summary>
/// Keeps the machine wired to what <see cref="IInputSetting"/> says, and keeps it that way.
/// </summary>
/// <remarks>
/// **The one thing that touches the graph on anybody's behalf.** The pages write a setting and
/// this watches it: when the setting moves the arrangement is made, and while it stands the
/// arrangement is held, because taking a source off its own output is not a thing that stays
/// done. The machine's session manager wires a stream back to the speakers whenever the stream
/// is remade, which is a new tab, a page reloaded, or a program moved between outputs.
///
/// **It has an owner that is not a page and not a window.** The holding used to live inside the
/// graph reading that the mixer and RECORD start when they are drawn and stop when they are put
/// away, so leaving both pages stopped it: with a browser lined up and Hear it off, which is the
/// quiet setting somebody uses before they need a source, the browser came back onto the speakers
/// a second or two later. On a show that is audio going out that nobody asked for.
///
/// What a page still owns is reading the graph to fill its picker, which really is about what is
/// on the screen and is right to stop when nothing is.
/// </remarks>
public interface IInputArrangement : IDisposable
{
    /// <summary>What came of the last arrangement, for whoever has to say so on the screen.</summary>
    InputAside Aside { get; }

    /// <summary>
    /// Said when a source had got back onto its own output and was taken off again.
    /// </summary>
    /// <remarks>
    /// **Worth saying on the screen and not only in a log.** It means the machine is undoing what
    /// this application arranged, which is a thing somebody working with it should see rather
    /// than find out about afterwards. Raised on this clock's own thread, so whoever shows it is
    /// the one that has to get it onto the drawing thread.
    /// </remarks>
    event Action<AudioRoute>? PutBack;

    /// <summary>
    /// Looks at the arrangement once and puts back anything that has crept out of it.
    /// </summary>
    /// <remarks>
    /// Called by its own clock, and by hand in a test. Nothing happens where nothing is supposed
    /// to be aside, which is every ordinary session.
    /// </remarks>
    void Check();
}
