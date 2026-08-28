using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using JingleBox2.Audio.Records;
using JingleBox2.ViewModels;
using System;

namespace JingleBox2.Views;

/// <summary>
/// The button that fetches a take off the shelf.
/// </summary>
/// <remarks>
/// A button and a dialog rather than pickers on the panel. Finding a take is a hunt: the
/// category it is filed under, the name you half remember, and the list itself with what each
/// one is. That is a page of controls, and a machine's front panel has room for one button.
///
/// Two pickers standing on the panel cost the width of two pickers whether or not you are
/// hunting, which is most of the time, and the panel is a fixed shape with a name field, a
/// keyboard and sixteen pads already on it.
///
/// The take is handed over and not held onto: this is a way of putting a recording somewhere,
/// not a place where the recording lives afterwards.
/// </remarks>
public class TakePicker : Button
{
    /// <summary>Which shelf the dialog opens on, and what it is allowed to offer.</summary>
    public static readonly StyledProperty<TakeFilter?> TakesProperty =
        AvaloniaProperty.Register<TakePicker, TakeFilter?>(nameof(Takes));

    /// <summary>What the button says while nothing has been picked through it.</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<TakePicker, string>(nameof(Placeholder), "Pick a recording...");

    /// <summary>A take has been picked. It is handed over once and not held onto.</summary>
    public event EventHandler<TakePickedEventArgs>? Picked;

    /// <summary>Binds the face to <see cref="Placeholder"/>, so the button says what it offers.</summary>
    public TakePicker()
    {
        this[!ContentProperty] = new Binding(nameof(Placeholder)) { Source = this };
    }

    /// <summary>Wears an ordinary button's clothes: a subclass has a theme of its own otherwise.</summary>
    protected override Type StyleKeyOverride => typeof(Button);

    /// <inheritdoc cref="TakesProperty"/>
    public TakeFilter? Takes
    {
        get => GetValue(TakesProperty);
        set => SetValue(TakesProperty, value);
    }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Opens the shelf and raises <see cref="Picked"/> when something came back from it.
    /// </summary>
    /// <remarks>
    /// A take with no path is not a take: the dialog can close on one while it is still being
    /// filled in, and handing that on would point whatever asked at a file that is not there.
    /// </remarks>
    protected override async void OnClick()
    {
        base.OnClick();

        if (Takes is not { } takes) return;

        var take = await TakeDialog.PickAsync(takes);

        if (take != null && take.FilePath.Length > 0) Picked?.Invoke(this, new TakePickedEventArgs(take));
    }
}

/// <summary>Which take was picked.</summary>
/// <param name="take">The recording, as the shelf holds it.</param>
public sealed class TakePickedEventArgs(Recording take) : EventArgs
{
    /// <summary>The recording that was picked, handed over once.</summary>
    public Recording Take { get; } = take;
}
