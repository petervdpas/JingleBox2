using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.Help;

namespace JingleBox2.Views;

/// <summary>
/// The help window: opened on whatever was asked about, and browsable from there.
/// </summary>
/// <remarks>
/// One window at a time. Asking again from somewhere else moves this one to that topic rather
/// than opening a second, which is what a help window that is left open should do.
/// </remarks>
public partial class HelpWindow : Window
{
    private static HelpWindow? _open;

    private ListBox? _topics;

    public HelpWindow()
    {
        InitializeComponent();

        _topics = this.FindControl<ListBox>("TopicList");
        if (_topics != null) _topics.ItemsSource = HelpText.All;
    }

    /// <summary>Shows the help for a topic, or the whole list when there is nothing for it.</summary>
    public static void Show(string? topicId, Window owner)
    {
        var topic = HelpText.Find(topicId) ?? (HelpText.All.Count > 0 ? HelpText.All[0] : null);
        if (topic == null || owner == null) return;

        if (_open != null)
        {
            _open.Go(topic);
            _open.Activate();
            return;
        }

        var window = new HelpWindow();
        window.Go(topic);

        _open = window;
        window.Closed += (_, _) => _open = null;

        window.Show(owner);
    }

    private void Go(HelpTopic topic)
    {
        DataContext = topic;

        if (_topics != null) _topics.SelectedItem = topic;
    }

    private void OnTopicChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_topics?.SelectedItem is HelpTopic topic) DataContext = topic;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
