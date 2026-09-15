using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Windows.Input;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.UI;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.Views;

/// <summary>
/// One toast: a message from the bus shown as a card in the application's own style.
/// </summary>
/// <remarks>
/// The heading is who said it, or the application's name where nobody signed it. Something
/// that worked, a warning and a fault colour the heading the way the bar lights its lamp for
/// them, through <see cref="IStatusLamp"/>, so one message reads the same in both; a plain
/// message keeps the card's own heading colour.
///
/// A click anywhere on the card takes it down, opening its link first where it has one, and a
/// hint under the words says so, since a card that opens a browser when touched ought to say it
/// will. The cross takes it down and opens nothing.
/// </remarks>
public partial class Toast : UserControl
{
    /// <summary>The message this toast shows.</summary>
    public static readonly StyledProperty<StatusMessage?> MessageProperty =
        AvaloniaProperty.Register<Toast, StatusMessage?>(nameof(Message));

    /// <summary>Run with <see cref="Message"/> when the toast is taken down.</summary>
    public static readonly StyledProperty<ICommand?> DismissCommandProperty =
        AvaloniaProperty.Register<Toast, ICommand?>(nameof(DismissCommand));

    /// <summary>The heading where nobody signed the message.</summary>
    public const string Unsigned = "JingleBox2";

    /// <summary>Which colour a kind of message is lit in, the same rule the bar asks.</summary>
    private static readonly IStatusLamp Lamps = new StatusLamp();

    /// <summary>Builds the card from its layout.</summary>
    public Toast()
    {
        InitializeComponent();
        Show();
    }

    /// <inheritdoc cref="MessageProperty"/>
    public StatusMessage? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <inheritdoc cref="DismissCommandProperty"/>
    public ICommand? DismissCommand
    {
        get => GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    /// <summary>Fills the card in again whenever the message changes.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MessageProperty) Show();
    }

    /// <summary>Puts the message's words on the card.</summary>
    private void Show()
    {
        var message = Message;

        Heading.Text = message is { From.Length: > 0 } ? message.From : Unsigned;
        Words.Text = message?.Text ?? "";
        LinkHint.IsVisible = message is { Link.Length: > 0 };

        if (message is { Kind: StatusKind.Done or StatusKind.Warning or StatusKind.Fault })
        {
            var palette = ThemePalette.From(this);
            Heading.Foreground = new SolidColorBrush(Lamps.For(message.Kind, palette.Accent, palette.Muted));
        }
        else
        {
            Heading.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    /// <summary>A press on the card: open the link where there is one, and take the toast down.</summary>
    /// <remarks>
    /// The cross answers its own press and marks it handled, so it never reaches here and opens
    /// nothing.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Handled || Message is not { } message || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        e.Handled = true;

        if (message.Link.Length > 0) Open(message.Link);

        Dismiss(message);
    }

    /// <summary>The cross: take the toast down and open nothing.</summary>
    private void OnDismiss(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (Message is { } message) Dismiss(message);
    }

    /// <summary>Asks whoever holds the toasts to take this one down.</summary>
    private void Dismiss(StatusMessage message)
    {
        if (DismissCommand?.CanExecute(message) == true) DismissCommand.Execute(message);
    }

    /// <summary>Opens a link in whatever the machine opens links with.</summary>
    /// <remarks>
    /// A link that is not an address, or a machine with nothing to open one, costs the opening
    /// and nothing else: the toast still comes down.
    /// </remarks>
    private async void Open(string link)
    {
        try
        {
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)) return;

            if (TopLevel.GetTopLevel(this) is { } top) await top.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception)
        {
        }
    }
}
