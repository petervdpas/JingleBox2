using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.Help;
using JingleBox2.Help.Records;
using JingleBox2.Help.Interfaces;

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
    /// <summary>Everything the app explains about itself, looked up by id.</summary>
    private static readonly IHelpText _help = new HelpText();

    /// <summary>The one that is open, so asking again moves it rather than opening a second.</summary>
    private static HelpWindow? _open;

    /// <summary>
    /// The list down the side, kept because it is both read from and written to: a topic asked
    /// for elsewhere has to select its row, and a row selected here has to become the page.
    /// </summary>
    private ListBox? _topics;

    /// <summary>Builds the window and fills its list with every topic there is.</summary>
    public HelpWindow()
    {
        InitializeComponent();

        _topics = this.FindControl<ListBox>("TopicList");
        if (_topics != null) _topics.ItemsSource = _help.All;
    }

    /// <summary>Shows the help for a topic, or the whole list when there is nothing for it.</summary>
    public static void Show(string? topicId, Window owner)
    {
        var topic = _help.Find(topicId) ?? (_help.All.Count > 0 ? _help.All[0] : null);
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

    /// <summary>
    /// Shows a topic, and moves the list to it, so arriving from a help badge leaves the list
    /// agreeing with the page rather than pointing at whatever was read last.
    /// </summary>
    private void Go(HelpTopic topic)
    {
        DataContext = topic;

        if (_topics != null) _topics.SelectedItem = topic;
    }

    /// <summary>Picking a row shows that topic. This is how the window is browsed.</summary>
    private void OnTopicChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_topics?.SelectedItem is HelpTopic topic) DataContext = topic;
    }

    /// <summary>Closes the window, which frees it to be opened fresh next time.</summary>
    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
