using System.Collections.ObjectModel;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// What the recording input is taken from, and what it could be taken from.
/// </summary>
/// <remarks>
/// The source is picked on the mixer, at the foot of the IN strip, because that is the strip it
/// is about: a strip says what a thing is doing and where it comes from is the first half of
/// that. RECORD shows the same answer as a line of words, since one choice said twice in two
/// pickers is two ways of doing one thing, which is the fault this codebase keeps naming.
///
/// Separate from <see cref="IInputWatch"/> although one class answers both, because they are
/// two different questions asked by two different pages. Watching is "somebody is showing the
/// meter, hold the input open", which is true of any page with a meter on it. This is "what is
/// feeding it", which is only true of a page that lets you say.
///
/// Reading the graph is in here rather than in the watching for the same reason it always was:
/// it puts the preferred source back when the system has wired something else up, which is
/// rewiring the machine's audio graph, and only a page carrying the picker has any business
/// doing that. What changed is which page that is.
/// </remarks>
public interface IInputSource
{
    /// <summary>Everything with audio to give right now, as the picker shows it.</summary>
    /// <remarks>
    /// Live rather than a snapshot: a program is only in the list while it is playing, so the
    /// list a picker is bound to is one that changes under it while it is open.
    /// </remarks>
    ObservableCollection<AudioRoute> Routes { get; }

    /// <summary>Which of them the input is being taken from, or nothing while none is chosen.</summary>
    /// <remarks>
    /// Written by somebody picking one and by the graph being read back, which is why setting it
    /// is not the same as choosing: see the implementation for how a reading is told from a
    /// choice.
    /// </remarks>
    AudioRoute? SelectedRoute { get; set; }

    /// <summary>False on a machine with no graph to patch, where the picker has nothing to offer.</summary>
    bool IsRoutingAvailable { get; }

    /// <summary>Reads the graph again, for a program that has started playing since.</summary>
    void RefreshRoutes();

    /// <summary>
    /// Says a page carrying the picker is on screen, so the graph is read and kept read.
    /// </summary>
    /// <remarks>
    /// Counted rather than switched, the same as <see cref="IInputWatch.Watch"/> and for the
    /// same reason: RECORD and the mixer are both entitled to ask, and a flag would have
    /// whichever page left last stop the reading under the page still up.
    /// </remarks>
    void WatchRoutes();

    /// <summary>Says one of those pages has gone.</summary>
    void LetRoutesGo();

    /// <summary>Whether this machine can take a source off everything but this application.</summary>
    /// <remarks>
    /// False is an ordinary answer about the machine rather than about this application: on a
    /// graph the links are moved, and where there is no graph a program can only be pointed at
    /// another output, so there has to be one to point it at.
    /// </remarks>
    bool CanTakeAside { get; }

    /// <summary>
    /// Whether the chosen source is taken off everything else, so it is heard through here alone.
    /// </summary>
    /// <remarks>
    /// **Capturing a source and taking it aside are two different acts.** Every program that
    /// records does the first; a browser captured is still playing out of the speakers, which is
    /// right for streaming and wrong on air. Off unless somebody says so, since it changes
    /// somebody else's program rather than this one.
    /// </remarks>
    bool TakeAside { get; set; }

    /// <summary>Whether a source has to be sent somewhere rather than simply unplugged.</summary>
    /// <remarks>
    /// True on a machine with no graph, where the only way to take a source off its own output
    /// is to point it at another one. The picker for that is drawn only there, since on a graph
    /// there is nothing to choose.
    /// </remarks>
    bool NeedsSilentOutput { get; }

    /// <summary>Every output a source could be sent to so nobody hears it.</summary>
    System.Collections.Generic.IReadOnlyList<Audio.Records.AudioEndpoint> SilentOutputs { get; }

    /// <summary>Which of them is chosen, or nothing while none is.</summary>
    /// <remarks>
    /// A cable is the usual answer and a spare socket is as good. Nothing chosen leaves the
    /// switch grey rather than failing when it is pressed, which is the rule every other switch
    /// on that strip keeps.
    /// </remarks>
    Audio.Records.AudioEndpoint? SilentOutput { get; set; }
}
