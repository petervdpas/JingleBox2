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
    private readonly Func<ITransportDeck> _page;
    private readonly IReadOnlyList<ITransportDeck> _decks;

    /// <summary>What was last shown, so a deck's ordinary chatter does not redraw the caps.</summary>
    private (bool, bool, bool, bool, bool, bool) _shown;

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

    public bool IsRecording => Wired.IsRecording;
    public bool IsPlaying => Wired.IsPlaying;
    public bool IsPaused => Wired.IsPaused;

    // Borrowed, the only cap with anything behind it is stop.
    public bool CanRecord => !Borrowed && Wired.CanRecord;
    public bool CanPlay => !Borrowed && Wired.CanPlay;
    public bool CanPause => !Borrowed && Wired.CanPause;

    public ICommand RecordCommand => new RelayCommand(() => { if (CanRecord) Wired.Record(); });
    public ICommand PlayCommand => new RelayCommand(() => { if (CanPlay) Wired.Play(); });
    public ICommand PauseCommand => new RelayCommand(() => { if (CanPause) Wired.Pause(); });
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
