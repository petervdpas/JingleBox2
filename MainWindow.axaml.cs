using Avalonia.Controls;
using Avalonia.Platform.Storage;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.ViewModels;
using System.Threading.Tasks;

namespace JingleBox2;

public partial class MainWindow : Window
{
    private readonly BassAudioEngine _audio = new(padCount: 8);
    private readonly ConfigStore _store = new("JingleBox2");

    public MainWindow()
    {
        InitializeComponent();

        async Task<string?> PickFileAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select sample",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Audio")
                    {
                        Patterns = new[] { "*.wav", "*.mp3", "*.ogg", "*.flac" }
                    }
                }
            });

            return files.Count == 1 ? files[0].Path.LocalPath : null;
        }

        var cfg = _store.LoadOrCreateDefault(padCount: 8);

        DataContext = new MainViewModel(_audio, PickFileAsync, _store, cfg);

        Closed += (_, __) => _audio.Dispose();
    }
}
