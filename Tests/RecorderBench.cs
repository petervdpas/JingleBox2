using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.Config;
using JingleBox2.ViewModels;

namespace JingleBox2.Tests;

/// <summary>
/// A RECORD page with nothing behind it, for the rules on it that are not about audio.
/// </summary>
/// <remarks>
/// The page takes six things and every one of them is a door onto hardware or a disc, so the
/// rules worth asking about, which source may be listened to and what a changed output does to a
/// source taken aside, could not be asked at all without this. Built once here rather than in
/// each test file, since two spellings of a double drift the same way two spellings of anything
/// else do.
/// </remarks>
public sealed class RecorderBench
{
    /// <summary>A source that is a program, which may be listened to.</summary>
    public static readonly AudioRoute Firefox = new("Firefox", "Firefox", AudioRouteKind.Application);

    /// <summary>What an output is playing, which may not: listening to it is a loop.</summary>
    public static readonly AudioRoute Speakers = new("Speakers.monitor", "Speakers", AudioRouteKind.Monitor);

    /// <summary>A capture device, which is the only kind that has ever been near a room.</summary>
    public static readonly AudioRoute Microphone = new("Mic", "Microphone", AudioRouteKind.Input);

    /// <summary>A routing that answers yes and writes down what it was asked.</summary>
    public sealed class Rewiring : IAudioRouting
    {
        /// <summary>How many times a source was taken off its own output.</summary>
        public int Aside { get; private set; }

        /// <summary>How many times whatever was taken aside was put back.</summary>
        public int Back { get; private set; }

        /// <inheritdoc/>
        public bool IsAvailable => true;

        /// <inheritdoc/>
        public IReadOnlyList<AudioRoute> GetRoutes() => new[] { Firefox, Speakers, Microphone };

        /// <summary>What was last connected, which is what the graph would then be showing.</summary>
        /// <remarks>
        /// **It remembers rather than answering one route for ever**, because the page reads the
        /// graph on a clock and puts the picker back to whatever the graph says is current. A
        /// double that always answered the same source made that reading a change: land it after
        /// a test had chosen another one and the picker flipped back, which showed up as one test
        /// failing about one time in three and passing on its own. The rule the real routing keeps
        /// is that connecting is what decides the current route, so the double keeps it too.
        /// </remarks>
        private AudioRoute _current = Firefox;

        /// <inheritdoc/>
        public AudioRoute? GetCurrentRoute() => _current;

        /// <summary>
        /// The machine pointing the capture somewhere nobody here chose.
        /// </summary>
        /// <remarks>
        /// What a session manager does whenever the stream is remade, and the one thing a reading
        /// of the graph can turn up that this application did not do itself. Said in the double
        /// rather than done through <see cref="Connect"/>, since going through there would be this
        /// application choosing it.
        /// </remarks>
        /// <param name="route">What the machine has wired the capture to.</param>
        public void Wired(AudioRoute route) => _current = route;

        /// <summary>How many times the capture was pointed at something.</summary>
        public int Connected { get; private set; }

        /// <inheritdoc/>
        public bool Connect(AudioRoute route)
        {
            Connected++;
            _current = route;

            return true;
        }

        /// <inheritdoc/>
        public bool CanTakeAside => true;

        /// <inheritdoc/>
        public string AsideNote => "";

        /// <inheritdoc/>
        public bool TakeAside(AudioRoute route)
        {
            Aside++;

            return true;
        }

        /// <summary>How many times the arrangement was held over what had crept back.</summary>
        public int Held { get; private set; }

        /// <summary>What the next hold answers, which is what a source creeping back looks like.</summary>
        public bool CreptBack { get; set; }

        /// <inheritdoc/>
        public bool HoldAside(AudioRoute route)
        {
            Held++;

            return CreptBack;
        }

        /// <inheritdoc/>
        public void GiveBack() => Back++;

        /// <summary>
        /// What this subsystem will say about whose output a monitor is.
        /// </summary>
        /// <remarks>
        /// Set by a test rather than worked out, since what is being exercised is what the page
        /// does with each of the three answers and not how any real subsystem reaches one.
        /// Nothing by default, which is the cautious answer and is what every test written before
        /// this one was leaning on.
        /// </remarks>
        public bool? Ours;

        /// <inheritdoc/>
        public bool? IsOurOutput(AudioRoute source, string? output) => Ours;
    }

    /// <summary>A recorder that captures nothing and remembers what it was told.</summary>
    public sealed class Deaf : IRecordingService
    {
        /// <inheritdoc/>
        /// <remarks>Written down so a test can read what the page decided about the source.</remarks>
        public bool HearsTheRoom { get; set; }

        /// <inheritdoc/>
        /// <remarks>Nothing rings here: there is no room and no speaker.</remarks>
        public event System.Action? Rang
        {
            add { }
            remove { }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetInputDevices() => Array.Empty<string>();

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
        public byte[] GetCapturedAudio() => Array.Empty<byte>();

        /// <inheritdoc/>
        public byte[] GetRecentRecordingData(int maxBytes) => Array.Empty<byte>();

        /// <inheritdoc/>
        public void ClearCapture() { }

        /// <inheritdoc/>
        public Task<SavedTake> WriteTakeAsync(string folder, string fileName, string cleanName) =>
            Task.FromResult(new SavedTake(string.Empty, null));

        /// <inheritdoc/>
        public JingleBox2.Audio.Plugins.Interfaces.IAudioInsert? Effect { get; set; }

        /// <inheritdoc/>
        public int SampleRate => 44100;

        /// <inheritdoc/>
        public int? LoopbackDevice { get; set; }

        /// <summary>What this machine is pretending to have playing out of it.</summary>
        public IReadOnlyList<LoopbackDevice> Outputs { get; set; } = Array.Empty<LoopbackDevice>();

        /// <summary>And what it is pretending is playing.</summary>
        public IReadOnlyList<AudioProgram> Playing { get; set; } = Array.Empty<AudioProgram>();

        /// <summary>How many times the outputs have really been walked.</summary>
        /// <remarks>
        /// Counted because on a real machine this is a walk of every audio endpoint through COM
        /// and takes a good part of a second, so how often it is asked is the thing under test
        /// rather than what it answers.
        /// </remarks>
        public int OutputWalks { get; private set; }

        /// <summary>And how many times the programs have been.</summary>
        /// <inheritdoc cref="OutputWalks" path="/remarks"/>
        public int ProgramWalks { get; private set; }

        /// <inheritdoc/>
        public IReadOnlyList<LoopbackDevice> GetLoopbackDevices()
        {
            OutputWalks++;

            return Outputs;
        }

        /// <inheritdoc/>
        public IReadOnlyList<AudioProgram> GetPrograms()
        {
            ProgramWalks++;

            return Playing;
        }

        /// <inheritdoc/>
        public int? LoopbackProgram { get; set; }

        /// <inheritdoc/>
        public void ReopenInput() { }

        /// <inheritdoc/>
        public void HearThrough(IMonitorFeed monitor) => Told = monitor;

        /// <summary>How many times the path was asked for again.</summary>
        public int Remade { get; private set; }

        /// <inheritdoc/>
        public void ReopenMonitor() => Remade++;

        /// <inheritdoc/>
        public void TakeFrom(IOutputBus bus) { }

        /// <summary>The path it was told about, so a test can say it was told once.</summary>
        public IMonitorFeed? Told { get; private set; }

        /// <inheritdoc/>
        public bool Hearing { get; set; }

        /// <inheritdoc/>
        public bool HearsCapture { get; set; } = true;
    }

    /// <summary>A meter that reads nothing.</summary>
    public sealed class Flat : ILevelMeterService
    {
        /// <inheritdoc/>
        public float GetLevelFromBytes(byte[]? data) => 0f;

        /// <inheritdoc/>
        public StereoLevel GetStereoFromBytes(byte[]? data, int channels) => new(0f, 0f);
    }

    /// <summary>A waveform service nothing here asks anything of.</summary>
    public sealed class Blank : IWaveformService
    {
        /// <inheritdoc/>
        public WaveformData AnalyzeFile(string filePath) => new() { PeakData = Array.Empty<float>() };

        /// <inheritdoc/>
        public TimeSpan GetDuration(string filePath) => TimeSpan.Zero;

        /// <inheritdoc/>
        public long GetFrameCount(string filePath) => 0;

        /// <inheritdoc/>
        public void TrimFile(string filePath, long startFrame, long endFrame) { }

        /// <inheritdoc/>
        public void SilenceFile(string filePath, long startFrame, long endFrame) { }

        /// <inheritdoc/>
        public void ReverseFile(string filePath, long startFrame, long endFrame) { }

        /// <inheritdoc/>
        public void FadeFile(string filePath, long startFrame, long endFrame, bool rising) { }

        /// <inheritdoc/>
        public double NormalizeFile(string filePath, double targetDecibels) => 0;
    }

    /// <summary>The recorder under the page, so a test can read what the page told it.</summary>
    public Deaf Recorder { get; } = new();

    /// <summary>The machine's wiring, so a test can count what was moved.</summary>
    public Rewiring Wiring { get; } = new();

    /// <summary>The settings block the page is built over.</summary>
    public SettingsBlock Block { get; } = new(new AppConfig());

    /// <summary>The settings themselves, so a test can name an input before the page opens.</summary>
    public AppConfig Settings => Block.Config;

    /// <summary>The page itself.</summary>
    public RecordViewModel Page { get; }

    /// <summary>
    /// Builds the page over the doubles, with the arrangement made where the test stands.
    /// </summary>
    /// <remarks>
    /// **The arrangement is handed in so that saying a thing and it having happened are one
    /// moment here.** In the application the setting is written by the drawing thread and the
    /// machine is made to match on a thread of its own, since that runs the graph's own tools; a
    /// test that asserted straight afterwards would be reading the answer to a question still in
    /// flight. The path is handed in with it because the two must be the same object: it
    /// remembers what it really moved, and a second one over the same routing would be a second
    /// memory of that.
    /// </remarks>
    /// <param name="routing">
    /// The wiring to build the page over, or nothing for <see cref="Wiring"/>, which answers yes
    /// to everything. Handed in for the tests that are about a routing which answers something
    /// else, since what the page does with a no is a rule of the page's.
    /// </param>
    public RecorderBench(IAudioRouting? routing = null)
    {
        var wiring = routing ?? Wiring;

        var setting = new InputSetting();
        var input = new InputPath(wiring);

        var arrangement = new InputArrangement(setting, input, work => work());

        Page = new RecordViewModel(
            Recorder, new Flat(), new Blank(), Block, wiring,
            setting: setting, input: input, arrangement: arrangement);
    }
}
