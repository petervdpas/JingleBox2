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

    bool IsRecording { get; }
    bool IsPlaying { get; }
    bool IsPaused { get; }

    bool CanRecord { get; }
    bool CanPlay { get; }
    bool CanPause { get; }

    void Record();
    void Play();
    void Pause();
    void Stop();
}
