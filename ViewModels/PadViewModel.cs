// ===============================
// ViewModels/PadViewModel.cs
// ===============================
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using System;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

public sealed partial class PadViewModel : ObservableObject
{
    private readonly IAudioEngine _audio;
    private readonly Func<Task<string?>> _pickFileAsync;

    public int Index { get; }

    public static PadSourceKind[] SourceKinds { get; } =
        new[] { PadSourceKind.File, PadSourceKind.StreamUrl };

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string? filePath;
    [ObservableProperty] private float volume = 1.0f;
    [ObservableProperty] private PadSourceKind sourceKind = PadSourceKind.File;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isPlaying;

    public bool IsFile => SourceKind == PadSourceKind.File;
    public bool IsWeb  => SourceKind == PadSourceKind.StreamUrl;

    public string Title => string.IsNullOrWhiteSpace(Name) ? $"Pad {Index + 1}" : Name;

    // CONFIG bindings
    public IRelayCommand PlayCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IAsyncRelayCommand AssignCommand { get; }

    // USE bindings
    public IRelayCommand TogglePlayCommand { get; }
    public IRelayCommand ClearCommand { get; }

    // USE helper
    public string UseButtonText => IsPlaying ? "STOP" : Title;
    public bool IsNotPlaying => !IsPlaying;

    public PadViewModel(int index, IAudioEngine audio, Func<Task<string?>> pickFileAsync)
    {
        Index = index;
        _audio = audio;
        _pickFileAsync = pickFileAsync;

        IsPlaying = _audio.IsPadPlaying(Index);

        _audio.PadPlaybackChanged += (s, e) =>
        {
            if (e.PadIndex != Index) return;

            IsPlaying = e.State == PadPlaybackState.Playing;

            if (e.State == PadPlaybackState.Error && !string.IsNullOrWhiteSpace(e.Message))
                Status = e.Message;

            OnPropertyChanged(nameof(UseButtonText));
            OnPropertyChanged(nameof(IsNotPlaying));
        };

        PlayCommand = new RelayCommand(() =>
        {
            Status = "";

            try
            {
                if (string.IsNullOrWhiteSpace(FilePath))
                {
                    Status = "No source set.";
                    return;
                }

                if (SourceKind == PadSourceKind.File)
                {
                    _audio.PlaySample(Index, FilePath, Volume);
                    Status = "Playing file.";
                }
                else if (SourceKind == PadSourceKind.StreamUrl)
                {
                    _audio.PlayStream(Index, FilePath, Volume);
                    Status = "Playing stream.";
                }
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        });

        StopCommand = new RelayCommand(() =>
        {
            Status = "";
            try
            {
                _audio.StopSample(Index);
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        });

        TogglePlayCommand = new RelayCommand(() =>
        {
            Status = "";

            try
            {
                if (IsPlaying)
                {
                    _audio.StopSample(Index);
                    return;
                }

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
            try
            {
                _audio.StopSample(Index);
                Name = "";
                FilePath = null;
                SourceKind = PadSourceKind.File;
                Volume = 1.0f;
                Status = "Cleared.";
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        });
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(UseButtonText));
    }

    partial void OnFilePathChanged(string? value) =>
        _audio.SetPadSource(Index, SourceKind, value);

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
}
