using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Enums;
using JingleBox2.Config.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins;

namespace JingleBox2.ViewModels;

/// <summary>
/// One pad: what it plays, how loud, what colour it is, and whether it is sounding now.
/// </summary>
/// <remarks>
/// The pad is the same object on PADS, where it is laid out, and on FIRE and USE, where it is
/// fired, so there is one answer to what a pad is doing rather than three views agreeing by
/// accident. It talks to <see cref="IAudioEngine"/> by its own index and nothing else: pads are
/// made and unmade while the application is running, and the engine holds against an index
/// outside its range rather than throwing.
///
/// Every setting writes through to the engine as it is set rather than at some later apply, so
/// a level dragged is heard while the hand is moving. The cost of that is the rate, which is
/// why the chain's own saving waits for the hand to stop; see <see cref="Patches"/>.
/// </remarks>
public sealed partial class PadViewModel : ObservableObject, IDisposable
{
    /// <summary>A chain of effects, written down and read back. Holds nothing, so one is enough.</summary>
    private readonly IPluginChainState _chains = new PluginChainState();

    /// <summary>The fader scale, so a reading in decibels can be checked without a window.</summary>
    private readonly IGainScale _gain = new GainScale();

    /// <summary>The sound, shared with every other pad and with the tracker.</summary>
    private readonly IAudioEngine _audio;

    /// <summary>
    /// Reads the progress, the meter and the fader back off the engine while a pad plays.
    /// </summary>
    /// <remarks>
    /// Twenty times a second, which is fast enough that a progress bar looks continuous and
    /// slow enough to cost nothing. It runs whether or not the pad is sounding, and writes
    /// noughts when it is not, so a pad that stopped does not leave its meter lit.
    /// </remarks>
    private readonly DispatcherTimer _progressTimer;

    /// <summary>
    /// The engine's playback event, kept so it can be taken off again in <see cref="Dispose"/>.
    /// </summary>
    /// <remarks>
    /// A resized matrix disposes the pads it no longer has, and a lambda that could not be
    /// unsubscribed would leave a dead pad listening to the engine for the rest of the session.
    /// </remarks>
    private readonly EventHandler<PadPlaybackChanged> _playbackHandler;

    /// <summary>Where this pad sits, counted from nought. It is also its index in the engine.</summary>
    public int Index { get; }

    /// <summary>
    /// What a hardware button is pointed at when the pointer is resting on this pad.
    /// </summary>
    /// <remarks>
    /// The same shape a mixer strip's six have: a template, copied before it is handed over,
    /// naming the pad and nothing about what is on it. A pad's own name is the recording somebody
    /// put on it this morning, and a link named after that would be a link that lies the moment
    /// the pad is filled with something else.
    /// </remarks>
    public Midi.ControlMapping PadLink => Midi.PadLinks.On(Index);

    /// <summary>
    /// The effect on this pad. Set once the pad knows which engine it plays through, so a pad
    /// built without one simply has no slot rather than a broken one.
    /// </summary>
    public PluginChainViewModel? Effect { get; private set; }

    /// <summary>Gives this pad its effect chain, pointed at itself.</summary>
    /// <param name="plugins">Everything installed, as scanned in SETTINGS.</param>
    /// <param name="effects">What effects of ours this installation has, which the plus offers first.</param>
    /// <param name="front">
    /// Where a face opened off this pad's chain says it is in front. It is the only answer there
    /// can be here: a pad is not a track, so nothing phrased as a track number ever reached an
    /// effect standing on one.
    /// </param>
    public void UsePlugins(
        PluginLibraryViewModel plugins,
        SoundDevices.SoundEffects.Interfaces.ISoundEffectProjects? effects = null,
        Interfaces.ISoundEffectInFront? front = null)
    {
        Effect = new PluginChainViewModel(plugins, effects, front: front)
        {
            Target = new PadPluginTarget(_audio, Index)
        };
        Effect.Changed += OnEffectChanged;

        OnPropertyChanged(nameof(Effect));
    }

    /// <summary>
    /// The chain changed, so the profile has something new to save. A knob dragged across its
    /// travel is a hundred of these, so the writing waits for the hand to stop.
    /// </summary>
    private void OnEffectChanged()
    {
        _effectSave.Stop();
        _effectSave.Start();
    }

    /// <summary>
    /// How long the chain has to be still before the pad is written down, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Long enough that a fader dragged across its travel is one save rather than a hundred,
    /// short enough that letting go and closing the application keeps the change.
    /// </remarks>
    private const int ChainSettleMs = 600;

    /// <summary>
    /// Restarted by every change to the chain, so it only ever fires once the hand has stopped.
    /// </summary>
    private readonly DispatcherTimer _effectSave =
        new() { Interval = TimeSpan.FromMilliseconds(ChainSettleMs) };

    /// <summary>
    /// What the plugins on this pad are holding inside themselves, by their place in the chain.
    /// </summary>
    /// <remarks>
    /// A preset is not a set of knob positions, so a chain saved as its parameters came back
    /// sounding roughly right and calling itself untitled. These are the rest of it.
    ///
    /// Read when the chain settles rather than when the pad is written down, because those are
    /// two very different rates: a pad is written on every property it has, and asking a plugin
    /// for its patch is a round trip to another process. The settling is also what makes the
    /// pad save at all, so what is kept here is never older than what is about to be written.
    /// </remarks>
    public IReadOnlyList<byte[]> Patches { get; private set; } = Array.Empty<byte[]>();

    /// <summary>
    /// Asks every plugin on the chain for its patch, which is a round trip apiece.
    /// </summary>
    /// <remarks>
    /// Called from the settling tick and from nowhere else, for the rate reasons on
    /// <see cref="Patches"/>. A pad with no chain answers an empty list rather than null, so
    /// whatever writes the pad down has nothing to test for.
    /// </remarks>
    private void ReadPatches()
    {
        Patches = Effect?.Target == null
            ? Array.Empty<byte[]>()
            : _chains.Patches(Effect.Target.Chain);
    }

    /// <summary>
    /// Puts back the effects this pad was saved with. A plugin that is no longer on the
    /// machine is named rather than passed over.
    /// </summary>
    /// <remarks>
    /// What was just put in is taken as what would be saved again, so a pad written down before
    /// its plugins are next touched keeps the patches it was opened with. Without that, opening
    /// the application and saving without touching a pad would strip the patches off it.
    /// </remarks>
    public void RestoreEffects(JingleBox2.Audio.Plugins.PluginChainConfig? saved)
    {
        if (Effect?.Target == null || saved == null || saved.IsEmpty) return;

        var missing = _chains.Restore(
            Effect.Target.Chain,
            saved,
            Effect.Target.SampleRate,
            PluginChainViewModel.MaxFrames);

        Effect.Reload();

        Patches = saved.Devices.Select(d => d.State).ToList();

        if (missing.Count > 0) Effect.Status = "Missing: " + string.Join(", ", missing);
    }

    /// <summary>
    /// The two things a pad can play, for the picker.
    /// </summary>
    /// <remarks>
    /// <see cref="PadSourceKind.None"/> is deliberately not on the list: it is what a pad
    /// nobody has touched is stored as, and it is read as a recording by
    /// <see cref="KindFor"/> rather than being a third choice somebody has to make.
    /// </remarks>
    public static PadSourceKind[] SourceKinds { get; } =
        new[] { PadSourceKind.Recording, PadSourceKind.Stream };

    /// <summary>
    /// The eight colours a pad can be given, in the order they are offered.
    /// </summary>
    /// <remarks>
    /// Red, orange, yellow, green, cyan, blue, purple and pink: one lap of the wheel, so any
    /// two of them read as different colours across a desk seen at a glance and from an angle.
    /// A pad may still hold any colour it was given; this is the palette, not the range.
    /// </remarks>
    public static readonly string[] PaletteColors =
    {
        "#E53935",
        "#FB8C00",
        "#FDD835",
        "#43A047",
        "#00ACC1",
        "#1E88E5",
        "#8E24AA",
        "#F06292",
    };

    /// <summary>What the pad is called. Empty means it is called by its number.</summary>
    [ObservableProperty] private string name = "";

    /// <summary>
    /// What it plays: a path to a recording, or the address of a stream.
    /// </summary>
    /// <remarks>
    /// A path either way, whichever kind it is, because that is what the engine is handed. What
    /// changed when the pads took their sound off the shelf is where the path comes from, not
    /// what is stored.
    /// </remarks>
    [ObservableProperty] private string? filePath;

    /// <summary>Its level as an amplitude, nought to one, which is what the engine multiplies by.</summary>
    [ObservableProperty] private float volume = 1.0f;

    /// <summary>A recording off the shelf, or a stream from the network.</summary>
    [ObservableProperty] private PadSourceKind sourceKind = PadSourceKind.Recording;

    /// <summary>Whether it starts again when it reaches the end.</summary>
    [ObservableProperty] private bool loop = false;

    /// <summary>How long it takes to come up to level, in seconds.</summary>
    [ObservableProperty] private double fadeIn = 0;

    /// <summary>And how long it takes to go quiet when it is stopped.</summary>
    [ObservableProperty] private double fadeOut = 0;

    /// <summary>Its colour as text, empty for a pad wearing whatever the theme gives it.</summary>
    [ObservableProperty] private string padColor = "";

    /// <summary>
    /// What went wrong, in words a person can read, or empty.
    /// </summary>
    /// <remarks>
    /// Cleared by every command before it tries anything, so a message on a pad is always about
    /// the last thing that was asked of it rather than about something from ten minutes ago.
    /// </remarks>
    [ObservableProperty] private string status = "";

    /// <summary>Whether it is sounding, which the engine says rather than the command that started it.</summary>
    [ObservableProperty] private bool isPlaying;

    /// <summary>How far through it is, nought to one, and nought when it is not playing.</summary>
    [ObservableProperty] private double playbackProgress;

    /// <summary>What it is putting out right now, for its own meter.</summary>
    [ObservableProperty] private float currentVolume;

    /// <summary>The level it is set to play at, which is the fader rather than the meter.</summary>
    [ObservableProperty] private float channelVolume;

    /// <summary>True when this pad plays a recording, for the layout to show the right editor.</summary>
    public bool IsRecording => SourceKind == PadSourceKind.Recording;

    /// <summary>And true when it plays a stream.</summary>
    public bool IsStream => SourceKind == PadSourceKind.Stream;

    /// <summary>
    /// What the pad plays, said as a name rather than as a path.
    /// </summary>
    /// <remarks>
    /// The take's own name is what you called it on the RECORD tab, and it is the whole of
    /// what is worth reading here. The path is still what is played and is on the tooltip, for
    /// a pad built before the pads took their sound off the shelf and pointing somewhere else
    /// entirely.
    /// </remarks>
    public string SourceText =>
        string.IsNullOrWhiteSpace(FilePath) ? "" : System.IO.Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>Whether there is a fade in worth showing a mark for.</summary>
    public bool HasFadeIn => FadeIn > 0;

    /// <summary>And whether there is a fade out.</summary>
    public bool HasFadeOut => FadeOut > 0;

    /// <summary>Whether anything is on this pad at all, which is what greys its transport.</summary>
    public bool HasSource => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>
    /// What is written on the pad: its name, or its number when it has none.
    /// </summary>
    /// <remarks>
    /// Counted from one here and from nought everywhere else, because the number on the face of
    /// a desk is what a person reads out loud.
    /// </remarks>
    public string Title => string.IsNullOrWhiteSpace(Name) ? $"Pad {Index + 1}" : Name;

    /// <summary>
    /// The pad's own colour, or null for the one the theme would give it.
    /// </summary>
    /// <remarks>
    /// Null rather than a fallback colour on purpose: the converter behind this reads null as
    /// "unset", which puts the theme's own style back. A colour that will not parse is read as
    /// none, since a pad painted from a broken setting is worse than one painted by the theme.
    ///
    /// **A pad keeps its own colour while it is playing**, and says it is playing by breathing
    /// instead. It used to hand its background back to the theme so the checked style could
    /// paint it, which cost the thing a wall of pads is for: every playing pad turned the same
    /// colour, so which one you had fired was a question about which one had changed rather than
    /// something you could see, and a pad whose own colour happened to be that one said nothing
    /// at all. Fired from a pad box, where several are going at once, it read as the colours
    /// having gone wrong.
    /// </remarks>
    public SolidColorBrush? PadBackground
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PadColor)) return null;

            try { return new SolidColorBrush(Color.Parse(PadColor)); }
            catch { return null; }
        }
    }

    /// <summary>
    /// The same colour as a colour, for a picker that deals in one rather than in a word.
    /// </summary>
    /// <remarks>
    /// Written back as six hex digits, which is what every pad colour in every settings file
    /// already is: the picker knows about transparency and a pad does not, since a pad you can
    /// see through is a pad that is the page with lettering on it.
    ///
    /// A pad with no colour reads as the plain grey the dot uses, so the picker opens on
    /// something rather than on black. Picking anything at all gives the pad a colour; the way
    /// back to having none is the clear button beside the palette, since a picker has no way to
    /// say "none" and one that tried would be a colour somebody could pick by accident.
    /// </remarks>
    public Color PadColorValue
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PadColor))
                try { return Color.Parse(PadColor); }
                catch { }

            return Color.FromRgb(80, 80, 80);
        }
        set => PadColor = "#"
            + value.R.ToString("X2", CultureInfo.InvariantCulture)
            + value.G.ToString("X2", CultureInfo.InvariantCulture)
            + value.B.ToString("X2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The same colour for the small dot on PADS, and always a brush.
    /// </summary>
    /// <remarks>
    /// The dot says which colour a pad has been given, so it cannot fall back to the theme the
    /// way <see cref="PadBackground"/> does: a dot showing the card behind it would say the pad
    /// has a colour when it has none. A pad with none, and one whose colour will not parse,
    /// both get the same grey.
    /// </remarks>
    public SolidColorBrush PadPreviewBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PadColor))
                try { return new SolidColorBrush(Color.Parse(PadColor)); }
                catch { }

            return new SolidColorBrush(Color.FromRgb(80, 80, 80));
        }
    }

    /// <summary>Starts the pad from the beginning. Says why in <see cref="Status"/> if it cannot.</summary>
    public IRelayCommand PlayCommand { get; }

    /// <summary>Stops it, fading out if it has been given a fade.</summary>
    public IRelayCommand StopCommand { get; }

    /// <summary>
    /// Starts it, or stops it if it is already sounding.
    /// </summary>
    /// <remarks>
    /// What a pad on the desk and a MIDI button both do, so the two cannot drift apart: a
    /// button that started a pad already playing would restart it under the presenter's hand.
    /// </remarks>
    public IRelayCommand TogglePlayCommand { get; }

    /// <summary>
    /// Empties the pad: stops it and puts every setting back to what a fresh pad has.
    /// </summary>
    /// <remarks>
    /// Its effect chain is deliberately left alone. Clearing is about what the pad plays, and a
    /// chain is set up once and kept, so taking it off with the sound would be a second edit
    /// nobody asked for.
    /// </remarks>
    public IRelayCommand ClearCommand { get; }

    /// <summary>Takes the pad's colour off, so the theme paints it again.</summary>
    public IRelayCommand ClearColorCommand { get; }

    /// <summary>Paints the pad, taking null as no colour rather than refusing it.</summary>
    public IRelayCommand<string?> SetColorCommand { get; }

    /// <summary>
    /// Builds a pad on a given place in the engine and starts reading it.
    /// </summary>
    /// <remarks>
    /// It asks the engine whether that pad is already playing rather than assuming it is not:
    /// the matrix is resized while the application runs, so a pad object can be built over a
    /// pad that is on air.
    /// </remarks>
    public PadViewModel(int index, IAudioEngine audio)
    {
        Index = index;
        _audio = audio;

        IsPlaying = _audio.IsPadPlaying(Index);

        _playbackHandler = (s, e) =>
        {
            if (e.PadIndex != Index) return;

            IsPlaying = e.State == PadPlaybackState.Playing;

            if (e.State == PadPlaybackState.Error && !string.IsNullOrWhiteSpace(e.Message))
                Status = e.Message;
        };
        _audio.PadPlaybackChanged += _playbackHandler;

        PlayCommand = new RelayCommand(() =>
        {
            Status = "";
            TryStart();
        });

        StopCommand = new RelayCommand(() =>
        {
            Status = "";
            TryStop();
        });

        TogglePlayCommand = new RelayCommand(() =>
        {
            Status = "";
            if (IsPlaying) TryStop();
            else TryStart();
        });

        ClearCommand = new RelayCommand(() =>
        {
            Status = "";
            try
            {
                _audio.StopSample(Index);
                Name = "";
                FilePath = null;
                SourceKind = PadSourceKind.Recording;
                Volume = 1.0f;
                Loop = false;
                FadeIn = 0;
                FadeOut = 0;
                PadColor = "";
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        });

        ClearColorCommand = new RelayCommand(() => PadColor = "");
        SetColorCommand   = new RelayCommand<string?>(color => PadColor = color ?? "");

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _progressTimer.Tick += (_, _) =>
        {
            if (IsPlaying)
            {
                PlaybackProgress = _audio.GetPadProgress(Index);
                CurrentVolume = _audio.GetPadLevel(Index);
                ChannelVolume = _audio.GetPadChannelVolume(Index);
            }
            else
            {
                PlaybackProgress = 0;
                CurrentVolume = 0;
                ChannelVolume = 0;
            }
        };
        _progressTimer.Start();

        _effectSave.Tick += (_, _) =>
        {
            _effectSave.Stop();

            ReadPatches();

            OnPropertyChanged(nameof(Effect));
        };
    }

    /// <summary>
    /// Plays whatever is on the pad, or says why it cannot.
    /// </summary>
    /// <remarks>
    /// A pad with nothing on it is an ordinary state rather than a fault, so it is answered in
    /// <see cref="Status"/> and not by throwing. A stream is started the same way and arrives
    /// when it arrives: nothing waits for it here.
    /// </remarks>
    private void TryStart()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                Status = "No source set.";
                return;
            }

            if (SourceKind == PadSourceKind.Recording)
                _audio.PlaySample(Index, FilePath, Volume);
            else
                _audio.PlayStream(Index, FilePath, Volume);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>Stops it, and puts whatever went wrong on the pad rather than throwing it.</summary>
    private void TryStop()
    {
        try
        {
            _audio.StopSample(Index);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>The name is half of <see cref="Title"/>, so the face has to be read again.</summary>
    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(Title));

    /// <summary>Points the engine at the new source, and re-reads what the pad says about it.</summary>
    partial void OnFilePathChanged(string? value)
    {
        _audio.SetPadSource(Index, SourceKind, value);
        OnPropertyChanged(nameof(HasSource));
        OnPropertyChanged(nameof(SourceText));
    }

    /// <summary>
    /// Holds the level inside nought to one and sends it to the engine at once.
    /// </summary>
    /// <remarks>
    /// Clamped rather than refused because this is written to from a fader, from a controller
    /// and from a stored profile, and a level outside the range is silence or distortion rather
    /// than an error anybody could act on.
    /// </remarks>
    partial void OnVolumeChanged(float value)
    {
        Volume = Math.Clamp(value, 0f, 1f);
        _audio.SetPadVolume(Index, Volume);

        OnPropertyChanged(nameof(VolumeDecibels));
    }

    /// <summary>
    /// The same level as a fader reads it.
    /// </summary>
    /// <remarks>
    /// A pad stores an amplitude, because that is what the engine multiplies by; a fader is
    /// marked in decibels with unity at nought, the way the mixer's are. Unity is the top of
    /// this one's travel rather than its middle: a pad plays a file, it does not amplify one,
    /// which is why the amplitude is clamped to 1 above.
    /// </remarks>
    public double VolumeDecibels
    {
        get => _gain.ToDecibels(Volume);
        set => Volume = (float)_gain.ToAmplitude(value);
    }

    /// <summary>
    /// Tells the engine what kind of thing the path is now, and re-reads the two flags the
    /// layout picks its editor from.
    /// </summary>
    partial void OnSourceKindChanged(PadSourceKind value)
    {
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsStream));
        OnPropertyChanged(nameof(SourceText));
        _audio.SetPadSource(Index, SourceKind, FilePath);
    }

    /// <summary>Takes effect on whatever the pad is playing now, not on the next thing.</summary>
    partial void OnLoopChanged(bool value) => _audio.SetPadLoop(Index, value);

    /// <summary>
    /// Holds the fade under five seconds and hands it to the engine.
    /// </summary>
    /// <remarks>
    /// The ceiling is 4.9 rather than 5 so the number a spinner can reach still reads as under
    /// five. A fade this long on a jingle is already past what anybody uses; the point of the
    /// bound is that a fade cannot be set to a length that outlasts the thing being faded.
    /// </remarks>
    partial void OnFadeInChanged(double value)
    {
        FadeIn = Math.Clamp(value, 0, 4.9);
        _audio.SetPadFadeIn(Index, FadeIn);
        OnPropertyChanged(nameof(HasFadeIn));
    }

    /// <summary>The other half of the fade, bounded the same way and for the same reason.</summary>
    partial void OnFadeOutChanged(double value)
    {
        FadeOut = Math.Clamp(value, 0, 4.9);
        _audio.SetPadFadeOut(Index, FadeOut);
        OnPropertyChanged(nameof(HasFadeOut));
    }

    /// <summary>Both brushes are worked out from the colour, so both have to be read again.</summary>
    partial void OnPadColorChanged(string value)
    {
        OnPropertyChanged(nameof(PadBackground));
        OnPropertyChanged(nameof(PadPreviewBrush));
        OnPropertyChanged(nameof(PadColorValue));
    }

    /// <summary>
    /// What a stored kind means on the page.
    /// </summary>
    /// <remarks>
    /// A pad with nothing on it is a pad waiting for a recording, so it is read as one rather
    /// than as a pad of no kind at all: the picker is then in front of you instead of behind a
    /// choice you have to make first. Nothing is written back, so a pad nobody has touched
    /// stays untouched in the profile.
    /// </remarks>
    public static PadSourceKind KindFor(PadSourceKind stored) =>
        stored == PadSourceKind.None ? PadSourceKind.Recording : stored;

    /// <summary>
    /// Puts a stored source on the pad, reading a missing kind through <see cref="KindFor"/>.
    /// </summary>
    /// <remarks>
    /// The kind goes on first: setting the path is what points the engine at it, and it points
    /// it at the kind the pad currently says it is.
    /// </remarks>
    public void SetSourceFromConfig(PadSourceKind kind, string source)
    {
        SourceKind = KindFor(kind);
        FilePath = string.IsNullOrWhiteSpace(source) ? null : source;
    }

    /// <summary>
    /// Stops reading the engine and lets go of it.
    /// </summary>
    /// <remarks>
    /// Called when the matrix shrinks and the pad goes away. What the pad was playing is the
    /// engine's business and is stopped there; a pad object going away is not a reason to take
    /// something off air.
    /// </remarks>
    public void Dispose()
    {
        _progressTimer.Stop();
        _audio.PadPlaybackChanged -= _playbackHandler;
    }
}
