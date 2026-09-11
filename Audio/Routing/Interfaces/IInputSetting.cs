using System;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing.Interfaces;

/// <summary>
/// What the input is set to: the two facts, in memory, and nothing about the machine.
/// </summary>
/// <remarks>
/// **This is the only thing the pages write to.** A picker and a tick box say what somebody
/// wants; what the machine is actually wired to is worked out from that by
/// <see cref="IInputArrangement"/>, which watches this and makes the graph match. The pages never
/// touch the graph, so which page is on screen decides nothing about where anybody's audio goes.
///
/// That is not tidiness. The arrangement used to be made and held inside the reading a page
/// starts when it is drawn and stops when it is put away, so changing tab with a browser lined up
/// and Hear it off let the browser back onto the speakers a second later, on air, with nothing on
/// the screen saying so.
///
/// In memory and never written down. A source chosen and a monitor switched on are about this
/// session: an application that started with somebody's browser already unplugged, or with a
/// microphone already open into the speakers, would be doing something nobody had asked for that
/// morning.
/// </remarks>
public interface IInputSetting
{
    /// <summary>What the input is pointed at, or nothing.</summary>
    AudioRoute? Source { get; }

    /// <summary>Whether what arrives is let into the mix.</summary>
    bool Heard { get; }

    /// <summary>What this application plays out of, by name, which decides what may be heard.</summary>
    /// <remarks>
    /// A setting like the other two rather than something asked of the engine when it is needed:
    /// what may be listened to depends on it, so whoever makes the arrangement has to be told
    /// when it moves in the same breath as the rest.
    /// </remarks>
    string? PlayingOut { get; }

    /// <summary>Said when any of the three moves, and never when they are said again unchanged.</summary>
    event Action? Changed;

    /// <summary>
    /// Says what the input is set to now.
    /// </summary>
    /// <remarks>
    /// All three at once, because they are one answer: a source with nothing to play it out of
    /// and a monitor with no source are half-answers, and applying them one at a time would make
    /// the machine follow a state nobody asked for on the way through.
    /// </remarks>
    /// <param name="source">What to point the input at, or nothing.</param>
    /// <param name="heard">Whether it is let into the mix.</param>
    /// <param name="playingOut">What this application plays out of, by name.</param>
    void Say(AudioRoute? source, bool heard, string? playingOut);
}
