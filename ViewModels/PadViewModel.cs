// ===============================
// ViewModels/PadViewModel.cs
// ===============================
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Models;

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
    [ObservableProperty] private bool loop = false;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isPlaying;

    public bool IsFile => SourceKind == PadSourceKind.File;
    public bool IsWeb => SourceKind == PadSourceKind.StreamUrl;

    public string Title => string.IsNullOrWhiteSpace(Name) ? $"Pad {Index + 1}" : Name;

    public IRelayCommand PlayCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IAsyncRelayCommand AssignCommand { get; }
    public IRelayCommand TogglePlayCommand { get; }
    public IRelayCommand ClearCommand { get; }

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
        };

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
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        });
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

    partial void OnLoopChanged(bool value) => _audio.SetPadLoop(Index, value);

    public void SetSourceFromConfig(PadSourceKind kind, string source)
    {
        SourceKind = kind;
        FilePath = string.IsNullOrWhiteSpace(source) ? null : source;
    }
}
