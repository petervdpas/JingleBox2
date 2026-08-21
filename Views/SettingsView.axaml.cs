using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opening a window is a view job, so it happens here rather than through the view model.
    /// </summary>
    private async void OnOpenPadMapping(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new MidiMappingWindow { DataContext = vm.Midi };
        await dialog.ShowDialog(owner);
    }
}
