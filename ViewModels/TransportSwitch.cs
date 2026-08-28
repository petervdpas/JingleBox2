using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace JingleBox2.ViewModels;

/// <summary>
/// What the transport is wired to at this moment.
/// </summary>
/// <remarks>
/// Two rules, and they are the whole of it.
///
/// The page you are on owns the transport. Whatever is running owns it more: leave the tracker
/// with the song playing, and the transport on RECORD is still the song, so you can see it is
/// running and stop it without going back. But a borrowed transport can only stop. Record and
/// play stay greyed, because the caps in front of you belong to a page you are not looking at,
/// and pressing play on them would start the wrong thing. Stop it and the transport hands back
/// to the page you are on, where play works again.
///
/// The page wins when both are going: recording a take while the song plays leaves the caps
/// on the take, which is the thing you are stood over.
/// </remarks>
public sealed class TransportSwitch : ObservableObject
{
    /// <summary>
    /// The deck of the page in front of you, asked for each time rather than held.
    /// </summary>
    /// <remarks>
    /// A function and not a deck, because which page you are on changes and a switch holding
    /// the deck it was built with would go on working the page you left.
    /// </remarks>
    private readonly Func<ITransportDeck> _page;

    /// <summary>
    /// Every deck there is, so the switch can find one that is running somewhere else.
    /// </summary>
    private readonly IReadOnlyList<ITransportDeck> _decks;

    /// <summary>What was last shown, so a deck's ordinary chatter does not redraw the caps.</summary>
    private (bool, bool, bool, bool, bool, bool) _shown;

    /// <summary>
    /// Listens to every deck, and takes what the caps show now as the starting point.
    /// </summary>
    /// <remarks>
    /// The subscriptions are never taken off, which is right here: the decks and the switch are
    /// both built once and live as long as the window does.
    /// </remarks>
    public TransportSwitch(Func<ITransportDeck> page, params ITransportDeck[] decks)
    {
        _page = page;
        _decks = decks;

        foreach (var deck in decks) deck.PropertyChanged += (_, _) => Recheck();

        _shown = Snapshot();
    }

    /// <summary>The deck of the page you are on.</summary>
    private ITransportDeck Page => _page();

    /// <summary>Something running that is not on this page, or null.</summary>
    private ITransportDeck? Elsewhere =>
        Page.IsRunning ? null : _decks.FirstOrDefault(d => d.IsRunning && !ReferenceEquals(d, Page));

    /// <summary>True while the caps belong to a page you are not looking at.</summary>
    public bool Borrowed => Elsewhere != null;

    /// <summary>The deck the caps are actually working.</summary>
    private ITransportDeck Wired => Elsewhere ?? Page;

    /// <summary>Whether the deck the caps are working is taking a recording.</summary>
    public bool IsRecording => Wired.IsRecording;

    /// <summary>Whether it is playing.</summary>
    public bool IsPlaying => Wired.IsPlaying;

    /// <summary>And whether it is sat paused.</summary>
    public bool IsPaused => Wired.IsPaused;

    /// <summary>
    /// Whether the record cap has anything behind it.
    /// </summary>
    /// <remarks>
    /// False whenever the transport is borrowed. The caps in front of you then belong to a page
    /// you are not looking at, and starting a recording on it is not what pressing them means.
    /// </remarks>
    public bool CanRecord => !Borrowed && Wired.CanRecord;

    /// <summary>Whether play does anything, which a borrowed transport never allows.</summary>
    public bool CanPlay => !Borrowed && Wired.CanPlay;

    /// <summary>And pause, on the same rule.</summary>
    public bool CanPause => !Borrowed && Wired.CanPause;

    /// <summary>
    /// Starts a recording on the wired deck, and does nothing when the cap is greyed.
    /// </summary>
    /// <remarks>
    /// The guard is inside the command rather than in a <c>CanExecute</c> because the caps are
    /// greyed from the properties above: a command that could not be executed would also have
    /// to be told, per deck, every time anything moved.
    /// </remarks>
    public ICommand RecordCommand => new RelayCommand(() => { if (CanRecord) Wired.Record(); });

    /// <summary>Plays the wired deck, and does nothing when the transport is borrowed.</summary>
    public ICommand PlayCommand => new RelayCommand(() => { if (CanPlay) Wired.Play(); });

    /// <summary>Pauses it, on the same rule.</summary>
    public ICommand PauseCommand => new RelayCommand(() => { if (CanPause) Wired.Pause(); });

    /// <summary>
    /// Stops whatever the caps are working, and this one is never greyed.
    /// </summary>
    /// <remarks>
    /// The whole point of a borrowed transport: leave the tracker with the song playing and you
    /// can still stop it from RECORD without going back for it.
    /// </remarks>
    public ICommand StopCommand => new RelayCommand(() => Wired.Stop());

    /// <summary>
    /// What the space bar does: stop whatever is running, and start what is in front of you
    /// when nothing is.
    /// </summary>
    /// <remarks>
    /// Nothing happens on a page with nothing to play, which is FIRE and is deliberate. A pad
    /// goes to air off its own pad and never off a key you leant on.
    /// </remarks>
    public void Toggle()
    {
        if (Wired.IsRunning)
        {
            Wired.Stop();
            return;
        }

        if (CanPlay) Wired.Play();
    }

    /// <summary>Told when the page changes, since that is what the caps are patched to.</summary>
    public void Moved() => Recheck();

    /// <summary>
    /// Everything the caps draw from, as one value, so two of them can be compared at once.
    /// </summary>
    private (bool, bool, bool, bool, bool, bool) Snapshot() =>
        (IsRecording, IsPlaying, IsPaused, CanRecord, CanPlay, CanPause);

    /// <summary>
    /// Redraws the caps, and only when they would look different.
    /// </summary>
    /// <remarks>
    /// A playing tracker says something about itself thirty times a second as the playhead
    /// moves, and none of it is about the transport.
    /// </remarks>
    private void Recheck()
    {
        var now = Snapshot();

        if (now.Equals(_shown)) return;

        _shown = now;

        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(CanRecord));
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(Borrowed));
    }
}
