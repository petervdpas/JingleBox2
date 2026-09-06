using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.Config;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A source taken aside is put back when the output moves.
/// </summary>
/// <remarks>
/// **What "only here" means depends on where here comes out.** The switch unplugs somebody
/// else's program from its own output on the promise that it is heard through this application
/// instead, and the output in SETTINGS is the whole of that second half. Picked another one and
/// the promise is over a device nobody is listening to, with the source still unplugged.
///
/// So the switch goes off and the machine is put back rather than the arrangement being carried
/// over to a device nobody asked it to be carried to. The unhappy path is the one that matters
/// as much: a device picked while nothing was taken aside must touch nothing at all, since that
/// is every ordinary run of this application.
/// </remarks>
public sealed class OutputMovedTests
{
    /// <summary>A routing that answers yes and writes down what it was asked.</summary>
    private sealed class Rewiring : IAudioRouting
    {
        public int Aside;
        public int Back;

        public bool IsAvailable => true;
        public IReadOnlyList<AudioRoute> GetRoutes() => new[] { Firefox };
        public AudioRoute? GetCurrentRoute() => Firefox;
        public bool Connect(AudioRoute route) => true;
        public bool CanTakeAside => true;

        public bool TakeAside(AudioRoute route)
        {
            Aside++;

            return true;
        }

        public void GiveBack() => Back++;
    }

    /// <summary>The source under test.</summary>
    private static readonly AudioRoute Firefox =
        new("Firefox", "Firefox", JingleBox2.Audio.Routing.Enums.AudioRouteKind.Application);

    /// <summary>A recorder that records nothing.</summary>
    private sealed class Deaf : IRecordingService
    {
        public IReadOnlyList<string> GetInputDevices() => Array.Empty<string>();
        public string? SelectedDevice { get; set; }
        public void StartRecording() { }
        public void StopRecording() { }
        public bool IsRecording => false;
        public void StartMonitoring() { }
        public void StopMonitoring() { }
        public bool IsMonitoring => false;
        public string? LastStartWarning => null;
        public double GainDb { get; set; }
        public int Channels => 2;
        public bool IsClipping => false;
        public bool ClippedDuringTake => false;
        public byte[] GetCapturedAudio() => Array.Empty<byte>();
        public byte[] GetRecentRecordingData(int maxBytes) => Array.Empty<byte>();
        public void ClearCapture() { }
        public Task<SavedTake> WriteTakeAsync(string folder, string fileName, string cleanName) =>
            Task.FromResult(new SavedTake(string.Empty, null));
        public JingleBox2.Audio.Plugins.Interfaces.IAudioInsert? Effect { get; set; }
        public int SampleRate => 44100;
        public int? LoopbackDevice { get; set; }
        public IReadOnlyList<LoopbackDevice> GetLoopbackDevices() => Array.Empty<LoopbackDevice>();
        public IReadOnlyList<AudioProgram> GetPrograms() => Array.Empty<AudioProgram>();
        public int? LoopbackProgram { get; set; }
        public void ReopenInput() { }
    }

    /// <summary>A meter that reads nothing.</summary>
    private sealed class Flat : ILevelMeterService
    {
        public float GetLevelFromBytes(byte[]? data) => 0f;
        public float GetLevelFromHandle(int channelHandle) => 0f;
        public StereoLevel GetStereoFromBytes(byte[]? data, int channels) => new(0f, 0f);
        public StereoLevel GetStereoFromHandle(int channelHandle) => new(0f, 0f);
    }

    /// <summary>A waveform service nothing asks anything of here.</summary>
    private sealed class Blank : IWaveformService
    {
        public WaveformData AnalyzeFile(string filePath) => new() { PeakData = Array.Empty<float>() };
        public void TrimFile(string filePath, long startFrame, long endFrame) { }
        public void SilenceFile(string filePath, long startFrame, long endFrame) { }
        public TimeSpan GetDuration(string filePath) => TimeSpan.Zero;
        public long GetFrameCount(string filePath) => 0;
        public double NormalizeFile(string filePath, double targetDecibels) => 0;
    }

    /// <summary>The page, over a routing that says yes.</summary>
    private static RecordViewModel Page(Rewiring wiring) =>
        new(new Deaf(), new Flat(), new Blank(), new ConfigStore(), new AppConfig(), wiring);

    /// <summary>A source taken aside is put back and the switch goes off.</summary>
    [Fact]
    public void The_output_moving_puts_a_source_back()
    {
        var wiring = new Rewiring();
        var page = Page(wiring);

        page.TakeAside = true;

        Assert.True(page.TakeAside);

        page.OutputMoved();

        Assert.False(page.TakeAside, "the switch stayed on over an output it was never set up for");
        Assert.True(wiring.Back > 0, "the machine was never put back");
    }

    /// <summary>And it says so, since a switch that turns itself off silently reads as a fault.</summary>
    [Fact]
    public void It_says_why_the_switch_went_off()
    {
        var wiring = new Rewiring();
        var page = Page(wiring);

        page.TakeAside = true;
        page.Status = string.Empty;

        page.OutputMoved();

        Assert.Contains("put back", page.Status, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nothing whatever happens where nothing was taken aside, which is the ordinary run.</summary>
    [Fact]
    public void An_output_moving_with_nothing_aside_touches_nothing()
    {
        var wiring = new Rewiring();
        var page = Page(wiring);

        page.Status = "still here";

        page.OutputMoved();

        Assert.False(page.TakeAside);
        Assert.Equal(0, wiring.Back);
        Assert.Equal("still here", page.Status);
    }
}
