using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JingleBox2.Views;

/// <summary>Hosts the pad mapping as a dialog, off the main tab strip.</summary>
public partial class MidiMappingWindow : Window
{
    public MidiMappingWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
