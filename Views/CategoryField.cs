using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JingleBox2.ViewModels;
using System;

namespace JingleBox2.Views;

/// <summary>
/// The box a take is filed in, wherever it is put.
/// </summary>
/// <remarks>
/// A control of its own rather than the same three handlers written out on the page and again
/// in the edit dialog, because none of the three is obvious and two copies would drift.
///
/// Typed rather than picked from a list, since a category is made by naming one. What has been
/// named already drops down as it is typed, so the second saxophone goes where the first one
/// went.
/// </remarks>
public class CategoryField : AutoCompleteBox
{
    public CategoryField()
    {
        FilterMode = AutoCompleteFilterMode.Contains;

        // Nothing typed still shows what there is, which is how a category is found rather
        // than remembered.
        MinimumPrefixLength = 0;

        PlaceholderText = "Uncategorized";

        SelectionChanged += Picked;
        LostFocus += Left;

        // Even when the box says it has dealt with the key, because with a suggestion showing
        // that is exactly what it says: Enter closes the list and fills the field, and a
        // handler that only hears unhandled keys never hears the one press that matters.
        AddHandler(KeyDownEvent, Entered, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Wears the ordinary box's clothes: a subclass has a theme of its own otherwise.</summary>
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    private RecordViewModel? Page => DataContext as RecordViewModel;

    /// <summary>A suggestion taken off the list is a category chosen, so it is filed at once.</summary>
    private void Picked(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectedItem is not string picked || picked.Length == 0) return;

        Page?.FileTakeUnder(picked);
    }

    private void Entered(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        Page?.FileTake();
    }

    /// <summary>
    /// Leaving the box files the take too, so a category typed and left is not lost.
    /// </summary>
    /// <remarks>
    /// A moment later, not now. The click that took the focus away may be on its way to a
    /// suggestion, and filing at the instant of the click would file the take under the two
    /// letters typed so far and invent a category out of a keystroke. By the time this runs,
    /// the suggestion is in the field.
    ///
    /// The click may equally have been on another take, and then it is the take the box was
    /// typed for that is filed, under what was typed for it, rather than the new one being
    /// given somebody else's category.
    /// </remarks>
    private void Left(object? sender, RoutedEventArgs e)
    {
        if (Page is not { } vm) return;

        var take = vm.SelectedRecording;
        string typed = vm.TakeCategory;

        Dispatcher.UIThread.Post(
            () => vm.FileUnder(take, ReferenceEquals(vm.SelectedRecording, take) ? vm.TakeCategory : typed),
            DispatcherPriority.Background);
    }
}
