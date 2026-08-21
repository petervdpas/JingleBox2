using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.Models;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The instrument bank and its editor. Shares the tracker's view model, so the list here and
/// the one beside the pattern are the same instruments with the same selection.
/// </summary>
public partial class InstrumentsView : UserControl
{
    public InstrumentsView()
    {
        InitializeComponent();
    }

    private TrackerViewModel? ViewModel => DataContext as TrackerViewModel;

    private void AddInstrument_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null && RecordingPicker.SelectedItem is Recording recording)
            ViewModel.AddInstrumentCommand.Execute(recording);
    }
}
