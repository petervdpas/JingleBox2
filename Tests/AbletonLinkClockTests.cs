using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JingleBox2.Sync.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Whether the transport really places its lines on an Ableton Link timeline: where a pass
/// begins, where each line after it falls, how far ahead it runs, and what it tells the session.
/// </summary>
/// <remarks>
/// A real <see cref="TrackerPlayer"/> over a silent engine and an output whose latency is nought,
/// so the only lead is the offset each test sets. The session is a double whose timeline is a
/// straight line of arithmetic, so where a beat falls is known exactly and what the clock thread
/// did can be read against it. The moments are taken on the session's own clock as each line is
/// announced, which is the clock the transport is meant to be keeping to.
///
/// Fast tempos, so a bar is a fraction of a second and a test is a moment rather than a wait.
/// The tolerances are loose enough for a busy test machine and tight enough to fail: a line on
/// the stopwatch's own path instead of the timeline's is a whole bar out.
/// </remarks>
public class AbletonLinkClockTests
{
    /// <summary>How late a line may be against its beat before it is wrong rather than busy.</summary>
    private const long LateMicros = 25_000;

    /// <summary>How early, which is much less, since the clock thread spins out its last moments.</summary>
    private const long EarlyMicros = 3_000;

    /// <summary>How long to wait for the clock to do something before giving up.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>A session with a timeline that is a straight line through a beat at a moment.</summary>
    private sealed class Session : IAbletonLink
    {
        /// <summary>Where this session's clock starts, so its readings are well away from nought.</summary>
        private const long Origin = 1_000_000_000;

        /// <summary>The stopwatch the clock is read off.</summary>
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>Held around the timeline, which a proposal moves while the clock thread reads it.</summary>
        private readonly object _lock = new();

        /// <summary>The moment the line passes through.</summary>
        private long _anchorTime = Origin;

        /// <summary>The beat at that moment.</summary>
        private double _anchorBeat;

        /// <summary>The tempo the line rises at.</summary>
        private double _tempo;

        /// <summary>A session at that tempo whose timeline is at that beat now.</summary>
        public Session(double tempo, double beatNow, int peers)
        {
            _tempo = tempo;
            _anchorBeat = beatNow;
            Peers = peers;
        }

        /// <summary>Every tempo proposed, in order.</summary>
        public readonly List<double> Proposed = new();

        /// <summary>Every start and stop told, in order.</summary>
        public readonly List<bool> Told = new();

        /// <inheritdoc/>
        public bool Present => true;

        /// <inheritdoc/>
        public string? Missing => null;

        /// <inheritdoc/>
        public bool IsOn { get; set; } = true;

        /// <inheritdoc/>
        public void Use(bool on) => IsOn = on;

        /// <inheritdoc/>
        public bool SharesStartStop { get; set; } = true;

        /// <inheritdoc/>
        public int OffsetMilliseconds { get; set; }

        /// <inheritdoc/>
        public double Quantum { get; set; } = 4;

        /// <inheritdoc/>
        public int Peers { get; set; }

        /// <inheritdoc/>
        public double Tempo
        {
            get { lock (_lock) return _tempo; }
        }

        /// <inheritdoc/>
        public long Now => Origin + (long)(_clock.Elapsed.TotalMilliseconds * 1000);

        /// <inheritdoc/>
        public double BeatAt(long micros, double quantum)
        {
            lock (_lock) return _anchorBeat + (micros - _anchorTime) * _tempo / 60_000_000.0;
        }

        /// <inheritdoc/>
        public long TimeAt(double beat, double quantum)
        {
            lock (_lock) return _anchorTime + (long)Math.Round((beat - _anchorBeat) * 60_000_000.0 / _tempo);
        }

        /// <inheritdoc/>
        public bool IsPlaying { get; private set; }

        /// <inheritdoc/>
        /// <remarks>Turns the line about the present moment, so the beat does not jump.</remarks>
        public void Propose(double bpm)
        {
            lock (_lock)
            {
                long now = Now;

                _anchorBeat = _anchorBeat + (now - _anchorTime) * _tempo / 60_000_000.0;
                _anchorTime = now;
                _tempo = bpm;

                Proposed.Add(bpm);
            }
        }

        /// <summary>
        /// Moves the whole timeline later by so many beats, which is what the session agreeing on a
        /// new phase looks like to a peer already in it.
        /// </summary>
        public void Delay(double beats)
        {
            lock (_lock) _anchorBeat -= beats;
        }

        /// <inheritdoc/>
        public void Play(bool playing)
        {
            lock (_lock)
            {
                if (!SharesStartStop || IsPlaying == playing) return;

                IsPlaying = playing;
                Told.Add(playing);
            }
        }

        /// <inheritdoc/>
        public event Action<int>? PeersMoved { add { } remove { } }

        /// <inheritdoc/>
        public event Action<double>? TempoHeard { add { } remove { } }

        /// <inheritdoc/>
        public event Action<bool>? PlayingHeard { add { } remove { } }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }

    /// <summary>A song of that many lines at a division worth measuring, its own tempo far from the session's.</summary>
    private static Song Of(int lines, int linesPerBeat = 4)
    {
        var song = new Song { Bpm = 77, LinesPerBeat = linesPerBeat };

        song.Patterns.Add(new Pattern(lines, song.TrackCount) { Name = "P" });
        song.Order.Add(0);
        song.Normalize();

        return song;
    }

    /// <summary>A player over a silent engine and an output with no latency, on that session.</summary>
    private static TrackerPlayer On(Session session) =>
        new(new SilentAudio(), output: new MixerBench()) { Loop = true, AbletonLink = session };

    /// <summary>Plays and writes down the session's clock at each of the first so many lines.</summary>
    private static List<long> Lines(TrackerPlayer player, Session session, int count, int lines = 64)
    {
        var heard = new List<long>();

        player.PositionChanged += (_, _) =>
        {
            lock (heard) heard.Add(session.Now);
        };

        player.Play(Of(lines), TrackerPosition.Start, TrackerPlayMode.Pattern);

        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            lock (heard)
            {
                if (heard.Count >= count) break;
            }

            Thread.Sleep(2);
        }

        player.Stop();

        lock (heard) return heard.Take(count).ToList();
    }

    /// <summary>A moment that should have been at another, held to the tolerances.</summary>
    private static void At(long expected, long actual, string what)
    {
        long off = actual - expected;

        Assert.True(off >= -EarlyMicros && off <= LateMicros,
            what + " was " + off / 1000.0 + " ms off its beat");
    }

    /// <summary>With another program in the session, the first line waits for the next bar.</summary>
    [Fact]
    public void With_peers_the_first_line_lands_on_the_next_bar()
    {
        var session = new Session(tempo: 240, beatNow: 1.3, peers: 1);

        using var player = On(session);

        var heard = Lines(player, session, 1);

        Assert.Single(heard);
        At(session.TimeAt(4, 4), heard[0], "line nought");
    }

    /// <summary>Alone, it starts at once rather than waiting for a bar nobody else is counting.</summary>
    [Fact]
    public void Alone_the_first_line_plays_at_once()
    {
        var session = new Session(tempo: 240, beatNow: 1.3, peers: 0);

        using var player = On(session);

        long asked = session.Now;
        var heard = Lines(player, session, 1);

        Assert.Single(heard);
        Assert.True(heard[0] - asked < 100_000, "alone, the first line waited " + (heard[0] - asked) / 1000.0 + " ms");
        Assert.True(heard[0] < session.TimeAt(4, 4) - 200_000, "alone, the first line waited for a bar");
    }

    /// <summary>Every line after the first falls where its beat does, at the session's tempo and not the song's.</summary>
    [Fact]
    public void Each_line_falls_on_its_own_beat()
    {
        var session = new Session(tempo: 240, beatNow: 0.5, peers: 1);

        using var player = On(session);

        var heard = Lines(player, session, 9);

        Assert.Equal(9, heard.Count);

        for (int i = 0; i < heard.Count; i++)
            At(session.TimeAt(4 + i * 0.25, 4), heard[i], "line " + i);
    }

    /// <summary>
    /// A timeline that moves under a running pass is followed from the next line, which is the
    /// whole difference between keeping to a session and keeping to a stopwatch started on it.
    /// </summary>
    /// <remarks>
    /// At a steady tempo a stopwatch started on the first beat lands on every later beat too, so
    /// the test above cannot tell the two apart. Moved a tenth of a beat later between lines two
    /// and three, a pass on the stopwatch plays every line after that 25 ms early.
    /// </remarks>
    [Fact]
    public void A_timeline_that_moves_is_followed()
    {
        var session = new Session(tempo: 240, beatNow: 1.0, peers: 1);

        using var player = On(session);

        int lines = 0;
        player.PositionChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref lines) == 3) session.Delay(0.1);
        };

        var heard = Lines(player, session, 7);

        Assert.Equal(7, heard.Count);

        for (int i = 3; i < heard.Count; i++)
            At(session.TimeAt(4 + i * 0.25, 4), heard[i], "line " + i);
    }

    /// <summary>The offset runs the lines that far ahead of the beat, which is what the output's lead is for.</summary>
    [Fact]
    public void The_offset_runs_the_lines_ahead()
    {
        var session = new Session(tempo: 240, beatNow: 1.0, peers: 1) { OffsetMilliseconds = 60 };

        using var player = On(session);

        var heard = Lines(player, session, 3);

        Assert.Equal(3, heard.Count);

        for (int i = 0; i < heard.Count; i++)
            At(session.TimeAt(4 + i * 0.25, 4) - 60_000, heard[i], "line " + i);
    }

    /// <summary>The tempo the transport says it plays at is the session's, whatever the song says.</summary>
    [Fact]
    public void The_playing_tempo_is_the_sessions()
    {
        var session = new Session(tempo: 180, beatNow: 0, peers: 0);

        using var player = On(session);

        player.Play(Of(64), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.Equal(180, player.PlayingBpm);

        player.Stop();
    }

    /// <summary>A tempo set while playing, by a lane or a hand, is put to the session.</summary>
    [Fact]
    public void A_tempo_set_while_playing_is_proposed()
    {
        var session = new Session(tempo: 120, beatNow: 0, peers: 0);

        using var player = On(session);

        player.Play(Of(64), TrackerPosition.Start, TrackerPlayMode.Pattern);
        player.PlayAt(133);

        Assert.Contains(133.0, session.Proposed);
        Assert.Equal(133, player.PlayingBpm);

        player.Stop();
    }

    /// <summary>Play, pause and stop are told to the session, each once.</summary>
    [Fact]
    public void Starting_and_stopping_are_told()
    {
        var session = new Session(tempo: 240, beatNow: 0, peers: 0);

        using var player = On(session);

        player.Play(Of(64), TrackerPosition.Start, TrackerPlayMode.Pattern);
        player.Stop();
        player.Stop();

        Assert.Equal(new[] { true, false }, session.Told);
    }

    /// <summary>A session that is off is not a clock: the transport keeps its own time and tells it nothing.</summary>
    [Fact]
    public void A_session_that_is_off_is_left_alone()
    {
        var session = new Session(tempo: 240, beatNow: 1.3, peers: 1) { IsOn = false };

        using var player = On(session);

        long asked = session.Now;
        var heard = Lines(player, session, 1);

        Assert.Single(heard);
        Assert.True(heard[0] - asked < 100_000, "an off session still held the first line back");
        Assert.Empty(session.Told);
        Assert.Equal(77, player.PlayingBpm);
    }
}
