using System.ComponentModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// What the four caps at the top of the window are working, on the page you are on.
/// </summary>
/// <remarks>
/// The transport is one control for the whole window, and what it means is different on every
/// page: a take on RECORD, the pads on FIRE, the song on TRACKER. Rather than four commands
/// hard-wired to the tracker and three pages quietly working the wrong thing, each page hands
/// the transport a deck, and the transport is patched to whichever deck is in front of you.
///
/// A deck says what it can do as well as what it is doing, so a cap with nothing behind it is
/// greyed rather than being a button that silently does nothing. FIRE's deck can only stop:
/// pads are fired by pads, and a space bar that could put a jingle to air is not one you want
/// near a desk that is on.
/// </remarks>
public interface ITransportDeck : INotifyPropertyChanged
{
    /// <summary>
    /// True while this deck is actually doing something: sounding, or taking a recording.
    /// </summary>
    /// <remarks>
    /// This is what decides who owns the transport when the thing that is running is not on
    /// the page you are looking at. Armed is not running: a tracker waiting for you to type
    /// notes is not making any sound.
    /// </remarks>
    bool IsRunning { get; }

    /// <summary>True while a recording is being taken.</summary>
    bool IsRecording { get; }

    /// <summary>True while something is sounding.</summary>
    bool IsPlaying { get; }

    /// <summary>
    /// True while what was playing is stopped where it stood.
    /// </summary>
    /// <remarks>
    /// Apart from not playing, because a deck that is paused still has a place in it: pressing
    /// play again carries on from there rather than starting at the top.
    /// </remarks>
    bool IsPaused { get; }

    /// <summary>
    /// Whether each cap has anything behind it on this deck.
    /// </summary>
    /// <remarks>
    /// A cap with nothing behind it is greyed rather than being a button that silently does
    /// nothing. FIRE's deck can only stop: pads are fired by pads, and a space bar that could put
    /// a jingle to air is not one you want near a desk that is on.
    /// </remarks>
    bool CanRecord { get; }

    /// <inheritdoc cref="CanRecord"/>
    bool CanPlay { get; }

    /// <inheritdoc cref="CanRecord"/>
    bool CanPause { get; }

    /// <summary>Starts taking a recording.</summary>
    void Record();

    /// <summary>Starts sounding, or carries on from where a pause left it.</summary>
    void Play();

    /// <summary>Stops where it stands, keeping the place.</summary>
    void Pause();

    /// <summary>
    /// Stops, and lets go of the place.
    /// </summary>
    /// <remarks>
    /// The one thing every deck can do, which is why it is the only cap FIRE offers: whatever a
    /// page is doing, there is always an answer to being asked to stop.
    /// </remarks>
    void Stop();
}
