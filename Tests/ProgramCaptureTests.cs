using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Recording one program on this machine: the third way to listen, and the one that makes
/// Windows behave the way Linux does.
/// </summary>
/// <remarks>
/// A PipeWire machine can point the recorder at a browser because every stream on it is
/// something that can be patched. Windows has no such graph, so per-process loopback stands in
/// for that one question, and what is asked here is everything around the call itself: which
/// prefix means what, that the three ways of listening cannot be on at once, and that whatever a
/// capture hands over arrives as the sixteen bit interleaved audio the rest of this application
/// is written against.
///
/// The activation itself is not exercised. It is a Windows call, the suite runs where there is
/// none, and a test that quietly passes because the platform said no reports nothing for the
/// rest of its life.
/// </remarks>
public class ProgramCaptureTests
{
    private readonly ISixteenBit _down = new SixteenBit();

    /// <summary>A recorder that answers plainly and writes down what was set on it.</summary>
    private sealed class Bench : IRecordingService
    {
        /// <summary>The programs it will say are playing.</summary>
        public List<AudioProgram> Playing { get; } = new();

        /// <summary>And the outputs.</summary>
        public List<LoopbackDevice> Outputs { get; } = new();

        /// <inheritdoc/>
        public IReadOnlyList<string> GetInputDevices() => new[] { "Built-in" };
        /// <inheritdoc/>
        public string? SelectedDevice { get; set; }
        /// <inheritdoc/>
        public void StartRecording() { }
        /// <inheritdoc/>
        public void StopRecording() { }
        /// <inheritdoc/>
        public bool IsRecording => false;
        /// <inheritdoc/>
        public void StartMonitoring() { }
        /// <inheritdoc/>
        public void StopMonitoring() { }
        /// <inheritdoc/>
        public bool IsMonitoring => false;
        /// <inheritdoc/>
        public string? LastStartWarning => null;
        /// <inheritdoc/>
        public double GainDb { get; set; }
        /// <inheritdoc/>
        public int Channels => 2;
        /// <inheritdoc/>
        public bool IsClipping => false;
        /// <inheritdoc/>
        public bool ClippedDuringTake => false;
        /// <inheritdoc/>
        public byte[] GetRecentRecordingData(int maxBytes) => Array.Empty<byte>();
        /// <inheritdoc/>
        public Task<SavedTake> WriteTakeAsync(string folder, string fileName, string cleanName) =>
            Task.FromResult(new SavedTake(string.Empty, null));
        /// <inheritdoc/>
        public JingleBox2.Audio.Plugins.Interfaces.IAudioInsert? Effect { get; set; }
        /// <inheritdoc/>
        public int SampleRate => 44100;
        /// <inheritdoc/>
        public int? LoopbackDevice { get; set; }
        /// <inheritdoc/>
        public int? LoopbackProgram { get; set; }
        /// <inheritdoc/>
        public IReadOnlyList<LoopbackDevice> GetLoopbackDevices() => Outputs;
        /// <inheritdoc/>
        public IReadOnlyList<AudioProgram> GetPrograms() => Playing;

        /// <summary>How many times the capture was told to close and open again.</summary>
        public int Reopened { get; private set; }

        /// <inheritdoc/>
        public void ReopenInput() => Reopened++;
    }

    /// <summary>A machine with no per-process capture offers none and says so.</summary>
    /// <remarks>
    /// Which is Linux, and nothing is lost by it: there a program is a node in the graph and the
    /// routing points the input straight at it.
    /// </remarks>
    [Fact]
    public void A_machine_without_it_offers_nothing()
    {
        var none = new NoProgramCapture();

        Assert.False(none.IsAvailable);
        Assert.Empty(none.Programs());
        Assert.False(none.Start(1234, _ => { }));
        Assert.False(none.IsRunning);
    }

    /// <summary>Stopping one that never started is an ordinary call rather than a fault.</summary>
    [Fact]
    public void Stopping_what_never_started_is_fine()
    {
        var none = new NoProgramCapture();

        none.Stop();
        none.Dispose();
    }

    /// <summary>This machine is asked once, and answers with something either way.</summary>
    /// <remarks>
    /// The whole point of the empty one: everything above holds a capture whatever the machine
    /// turns out to be, so nothing has to remember to check for a null.
    /// </remarks>
    [Fact]
    public void The_machine_is_asked_and_always_answers()
    {
        Assert.NotNull(new AudioCapture().Programs());
    }

    /// <summary>Every program that is playing is offered as a route of its own.</summary>
    [Fact]
    public void A_playing_program_is_offered()
    {
        var bench = new Bench();
        bench.Playing.Add(new AudioProgram(4321, "firefox"));

        var routes = new WindowsLoopbackRouting(bench).GetRoutes();

        if (!OperatingSystem.IsWindows())
        {
            Assert.Empty(routes);

            return;
        }

        var firefox = Assert.Single(routes, r => r.Name == "firefox");

        Assert.Equal(AudioRouteKind.Application, firefox.Kind);
    }

    /// <summary>The three ways of listening are told apart by what a route's address begins with.</summary>
    /// <remarks>
    /// The addresses are this application's own and are never shown, so what matters is that
    /// each one means exactly one path: a device, an output's playback, or a program.
    /// </remarks>
    [Fact]
    public void The_three_paths_have_their_own_addresses()
    {
        var bench = new Bench();
        bench.Playing.Add(new AudioProgram(4321, "firefox"));

        var routes = new WindowsLoopbackRouting(bench).GetRoutes();

        if (!OperatingSystem.IsWindows()) return;

        Assert.All(routes, r => Assert.True(
            r.Node.StartsWith("device:", StringComparison.Ordinal) ||
            r.Node.StartsWith("loopback:", StringComparison.Ordinal) ||
            r.Node.StartsWith("program:", StringComparison.Ordinal)));
    }

    /// <summary>Picking a program clears the output, and picking an output clears the program.</summary>
    /// <remarks>
    /// **The one that would fail quietly.** A program wins over an output on the way in, so a
    /// stale program id left behind would outlive the choice that replaced it: somebody picks
    /// their sound card and goes on recording the browser.
    /// </remarks>
    [Fact]
    public void Each_choice_clears_the_other_two()
    {
        if (!OperatingSystem.IsWindows()) return;

        var bench = new Bench();
        bench.Playing.Add(new AudioProgram(4321, "firefox"));
        bench.Outputs.Add(new LoopbackDevice(3, "Speakers"));

        var routing = new WindowsLoopbackRouting(bench);
        var routes = routing.GetRoutes();

        routing.Connect(routes.Single(r => r.Node == "program:4321"));

        Assert.Equal(4321, bench.LoopbackProgram);
        Assert.Null(bench.LoopbackDevice);

        routing.Connect(routes.Single(r => r.Node == "loopback:3"));

        Assert.Null(bench.LoopbackProgram);
        Assert.Equal(3, bench.LoopbackDevice);

        routing.Connect(routes.First(r => r.Node.StartsWith("device:", StringComparison.Ordinal)));

        Assert.Null(bench.LoopbackProgram);
        Assert.Null(bench.LoopbackDevice);
    }

    /// <summary>An address that is not a number is refused rather than read as nought.</summary>
    /// <remarks>
    /// Process nought is the system idle process on Windows, so a route whose id would not parse
    /// must not fall through to it.
    /// </remarks>
    [Fact]
    public void An_address_that_is_not_a_number_is_refused()
    {
        var bench = new Bench();

        var routing = new WindowsLoopbackRouting(bench);

        Assert.False(routing.Connect(new AudioRoute("program:not-a-number", "?", AudioRouteKind.Application)));
        Assert.Null(bench.LoopbackProgram);
    }

    /// <summary>Sixteen bit audio is handed straight back, and as its own array.</summary>
    /// <remarks>
    /// It is the shape everything above meets, so there is nothing to do to it. Its own array
    /// because a capture goes on using its buffer for the next block while whatever was handed
    /// the last one still holds it.
    /// </remarks>
    [Fact]
    public void Sixteen_bit_comes_back_as_it_was()
    {
        var block = new byte[] { 1, 2, 3, 4 };

        var down = _down.Down(block, block.Length, new CaptureFormat(44100, 2, 16, false));

        Assert.Equal(block, down);
        Assert.NotSame(block, down);
    }

    /// <summary>A float at full scale comes down to full scale, both ways.</summary>
    [Fact]
    public void Full_scale_floats_land_at_full_scale()
    {
        var block = new byte[8];

        BitConverter.GetBytes(1f).CopyTo(block, 0);
        BitConverter.GetBytes(-1f).CopyTo(block, 4);

        var down = _down.Down(block, block.Length, new CaptureFormat(44100, 2, 32, true));

        Assert.Equal(short.MaxValue, BitConverter.ToInt16(down, 0));
        Assert.Equal(short.MinValue, BitConverter.ToInt16(down, 2));
    }

    /// <summary>A float past full scale is held there rather than wrapping.</summary>
    /// <remarks>
    /// The one difference that matters. Wrapped, a signal a hair over the top comes out as a
    /// crack at the opposite extreme, which is the loudest thing a converter can be handed; held,
    /// it is what every converter does anyway.
    /// </remarks>
    [Fact]
    public void A_float_past_full_scale_is_held_rather_than_wrapped()
    {
        var block = new byte[8];

        BitConverter.GetBytes(4f).CopyTo(block, 0);
        BitConverter.GetBytes(-4f).CopyTo(block, 4);

        var down = _down.Down(block, block.Length, new CaptureFormat(44100, 2, 32, true));

        Assert.Equal(short.MaxValue, BitConverter.ToInt16(down, 0));
        Assert.Equal(short.MinValue, BitConverter.ToInt16(down, 2));
    }

    /// <summary>Silence comes through as silence.</summary>
    [Fact]
    public void Silence_stays_silent()
    {
        var block = new byte[8];

        var down = _down.Down(block, block.Length, new CaptureFormat(44100, 2, 32, true));

        Assert.Equal(4, down.Length);
        Assert.All(down, b => Assert.Equal(0, b));
    }

    /// <summary>Anything that is not a number is written as silence.</summary>
    /// <remarks>
    /// The same rule the take effects keep, and it matters more here than at the converters: a
    /// take full of it plays as full scale noise the first time anybody opens it.
    /// </remarks>
    [Fact]
    public void What_is_not_a_number_is_written_as_silence()
    {
        var block = new byte[4];

        BitConverter.GetBytes(float.NaN).CopyTo(block, 0);

        var down = _down.Down(block, block.Length, new CaptureFormat(44100, 2, 32, true));

        Assert.Equal(0, BitConverter.ToInt16(down, 0));
    }

    /// <summary>Half scale comes back at half scale, so nothing is quietly rescaled.</summary>
    [Fact]
    public void Half_scale_stays_half_scale()
    {
        var block = new byte[4];

        BitConverter.GetBytes(0.5f).CopyTo(block, 0);

        var down = _down.Down(block, block.Length, new CaptureFormat(44100, 2, 32, true));

        Assert.Equal(16384, BitConverter.ToInt16(down, 0));
    }

    /// <summary>A block cut off part way through a sample is read as far as it goes.</summary>
    /// <remarks>
    /// Whatever is handed over has to leave whole frames behind it, or every sample after the
    /// join has its bytes the wrong way round and the take is noise from that point on.
    /// </remarks>
    [Fact]
    public void A_block_cut_short_leaves_whole_samples()
    {
        var block = new byte[] { 1, 2, 3, 4, 5 };

        var down = _down.Down(block, block.Length, new CaptureFormat(44100, 2, 16, false));

        Assert.Equal(4, down.Length);
    }

    /// <summary>Only what is said to be real is read, however long the buffer is.</summary>
    /// <remarks>
    /// A capture hands over a buffer of its own size with a count of what is in it, so reading
    /// to the end of the array puts whatever was in it last time onto the end of the take.
    /// </remarks>
    [Fact]
    public void Only_what_is_real_is_read()
    {
        var block = new byte[64];

        var down = _down.Down(block, 8, new CaptureFormat(44100, 2, 16, false));

        Assert.Equal(8, down.Length);
    }

    /// <summary>And a count past the end of the buffer is held to the buffer.</summary>
    [Fact]
    public void A_count_past_the_end_is_held_to_the_buffer()
    {
        var block = new byte[8];

        Assert.Equal(8, _down.Down(block, 4000, new CaptureFormat(44100, 2, 16, false)).Length);
    }

    /// <summary>Nothing to read is an ordinary answer.</summary>
    [Fact]
    public void Nothing_to_read_is_an_ordinary_answer()
    {
        var format = new CaptureFormat(44100, 2, 16, false);

        Assert.Empty(_down.Down(Array.Empty<byte>(), 0, format));
        Assert.Empty(_down.Down(null!, 4, format));
        Assert.Empty(_down.Down(new byte[8], -1, format));
    }

    /// <summary>A shape nobody here can read comes back empty rather than as noise.</summary>
    /// <remarks>
    /// Guessing at it would put a take on the shelf that plays as a scream. Empty is a take of
    /// nothing, which is plainly wrong and harms nobody's ears.
    /// </remarks>
    [Fact]
    public void A_shape_nobody_can_read_comes_back_empty()
    {
        Assert.Empty(_down.Down(new byte[8], 8, new CaptureFormat(44100, 2, 8, false)));
        Assert.Empty(_down.Down(new byte[8], 8, new CaptureFormat(44100, 2, 64, true)));
    }

    /// <summary>Wider integers keep their top two bytes.</summary>
    [Fact]
    public void Wider_integers_keep_their_top_bytes()
    {
        var thirty_two = new byte[] { 0x11, 0x22, 0x33, 0x44 };

        Assert.Equal(
            new byte[] { 0x33, 0x44 },
            _down.Down(thirty_two, 4, new CaptureFormat(44100, 2, 32, false)));

        var twenty_four = new byte[] { 0x11, 0x22, 0x33 };

        Assert.Equal(
            new byte[] { 0x22, 0x33 },
            _down.Down(twenty_four, 3, new CaptureFormat(44100, 2, 24, false)));
    }
}
