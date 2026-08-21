// ===============================
// ViewModels/PadViewModel.cs
// ===============================
using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Models;

namespace JingleBox2.ViewModels;

public sealed partial class PadViewModel : ObservableObject, IDisposable
{
    private readonly IAudioEngine _audio;
    private readonly Func<Task<string?>> _pickFileAsync;
    private readonly DispatcherTimer _progressTimer;
    private readonly EventHandler<PadPlaybackChanged> _playbackHandler;

    public int Index { get; }

    /// <summary>
    /// The effect on this pad. Set once the pad knows which engine it plays through, so a pad
    /// built without one simply has no slot rather than a broken one.
    /// </summary>
    public PluginChainViewModel? Effect { get; private set; }

    /// <summary>Gives this pad its effect chain, pointed at itself.</summary>
    public void UsePlugins(PluginLibraryViewModel plugins)
    {
        Effect = new PluginChainViewModel(plugins) { Target = new PadPluginTarget(_audio, Index) };
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

    private readonly DispatcherTimer _effectSave = new() { Interval = TimeSpan.FromMilliseconds(600) };

    /// <summary>
    /// Puts back the effects this pad was saved with. A plugin that is no longer on the
    /// machine is named rather than passed over.
    /// </summary>
    public void RestoreEffects(JingleBox2.Audio.Plugins.PluginChainConfig? saved)
    {
        if (Effect?.Target == null || saved == null || saved.IsEmpty) return;

        var missing = JingleBox2.Audio.Plugins.PluginChainState.Restore(
            Effect.Target.Chain,
            saved,
            Effect.Target.SampleRate,
            PluginChainViewModel.MaxFrames);

        Effect.Reload();

        if (missing.Count > 0) Effect.Status = "Missing: " + string.Join(", ", missing);
    }

    public static PadSourceKind[] SourceKinds { get; } =
        new[] { PadSourceKind.File, PadSourceKind.StreamUrl };

    public static readonly string[] PaletteColors =
    {
        "#E53935", // red
        "#FB8C00", // orange
        "#FDD835", // yellow
        "#43A047", // green
        "#00ACC1", // cyan
        "#1E88E5", // blue
        "#8E24AA", // purple
        "#F06292", // pink
    };

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string? filePath;
    [ObservableProperty] private float volume = 1.0f;
    [ObservableProperty] private PadSourceKind sourceKind = PadSourceKind.File;
    [ObservableProperty] private bool loop = false;
    [ObservableProperty] private double fadeIn = 0;
    [ObservableProperty] private double fadeOut = 0;
    [ObservableProperty] private string padColor = "";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private double playbackProgress;
    [ObservableProperty] private float currentVolume;
    [ObservableProperty] private float channelVolume;

    public bool IsFile => SourceKind == PadSourceKind.File;
    public bool IsWeb => SourceKind == PadSourceKind.StreamUrl;
    public bool HasFadeIn => FadeIn > 0;
    public bool HasFadeOut => FadeOut > 0;
    public bool HasSource => !string.IsNullOrWhiteSpace(FilePath);

    public string Title => string.IsNullOrWhiteSpace(Name) ? $"Pad {Index + 1}" : Name;

    /// <summary>
    /// Returns a brush for the custom pad color, or null when no color is set / pad is playing
    /// (null causes the NullToUnset converter to restore the theme style).
    /// </summary>
    public SolidColorBrush? PadBackground
    {
        get
        {
            // While playing, let the :checked style handle the background
            if (IsPlaying || string.IsNullOrWhiteSpace(PadColor))
                return null;

            try { return new SolidColorBrush(Color.Parse(PadColor)); }
            catch { return null; }
        }
    }

    /// <summary>
    /// Always returns a brush: grey when no color is configured, custom color otherwise.
    /// Used for the small preview dot in PadsView.
    /// </summary>
    public SolidColorBrush PadPreviewBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PadColor))
                try { return new SolidColorBrush(Color.Parse(PadColor)); }
                catch { /* fall through */ }

            return new SolidColorBrush(Color.FromRgb(80, 80, 80));
        }
    }

    public IRelayCommand PlayCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IAsyncRelayCommand AssignCommand { get; }
    public IRelayCommand TogglePlayCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand ClearColorCommand { get; }
    public IRelayCommand<string?> SetColorCommand { get; }

    public PadViewModel(int index, IAudioEngine audio, Func<Task<string?>> pickFileAsync)
    {
        Index = index;
        _audio = audio;
        _pickFileAsync = pickFileAsync;

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

        AssignCommand = new AsyncRelayCommand(async () =>
        {
            Status = "";

            if (SourceKind != PadSourceKind.File)
            {
                Status = "Browse is only available for File.";
                return;
            }

            var path = await _pickFileAsync();
            if (!string.IsNullOrWhiteSpace(path))
                FilePath = path;
        });

        ClearCommand = new RelayCommand(() =>
        {
            Status = "";
            try
            {
                _audio.StopSample(Index);
                Name = "";
                FilePath = null;
                SourceKind = PadSourceKind.File;
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

        // Fires once the hand has come off the knob, and is what makes the profile save.
        _effectSave.Tick += (_, _) =>
        {
            _effectSave.Stop();
            OnPropertyChanged(nameof(Effect));
        };
    }

    private void TryStart()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                Status = "No source set.";
                return;
            }

            if (SourceKind == PadSourceKind.File)
                _audio.PlaySample(Index, FilePath, Volume);
            else
                _audio.PlayStream(Index, FilePath, Volume);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

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

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(Title));

    partial void OnFilePathChanged(string? value)
    {
        _audio.SetPadSource(Index, SourceKind, value);
        OnPropertyChanged(nameof(HasSource));
    }

    partial void OnVolumeChanged(float value)
    {
        Volume = Math.Clamp(value, 0f, 1f);
        _audio.SetPadVolume(Index, Volume);
    }

    partial void OnSourceKindChanged(PadSourceKind value)
    {
        OnPropertyChanged(nameof(IsFile));
        OnPropertyChanged(nameof(IsWeb));
        _audio.SetPadSource(Index, SourceKind, FilePath);
    }

    partial void OnLoopChanged(bool value) => _audio.SetPadLoop(Index, value);

    partial void OnFadeInChanged(double value)
    {
        FadeIn = Math.Clamp(value, 0, 4.9);
        _audio.SetPadFadeIn(Index, FadeIn);
        OnPropertyChanged(nameof(HasFadeIn));
    }

    partial void OnFadeOutChanged(double value)
    {
        FadeOut = Math.Clamp(value, 0, 4.9);
        _audio.SetPadFadeOut(Index, FadeOut);
        OnPropertyChanged(nameof(HasFadeOut));
    }

    partial void OnPadColorChanged(string value)
    {
        OnPropertyChanged(nameof(PadBackground));
        OnPropertyChanged(nameof(PadPreviewBrush));
    }

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PadBackground));

    public void SetSourceFromConfig(PadSourceKind kind, string source)
    {
        SourceKind = kind;
        FilePath = string.IsNullOrWhiteSpace(source) ? null : source;
    }

    public void Dispose()
    {
        _progressTimer.Stop();
        _audio.PadPlaybackChanged -= _playbackHandler;
    }
}
