using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JingleBox2.Midi;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>One layer of controller links, as a heading and a card for each thing.</summary>
/// <remarks>
/// Bound to a <see cref="ControlLinksViewModel"/> itself rather than to something holding one,
/// so the three places that want a list of links all point one of these at the layer they are
/// about and nothing here has to know which layer it is drawing.
///
/// The two buttons are here rather than commands on the view model because both of them are a
/// file picker, which is the window's and not the layer's. Everything either one does once a
/// path is known is the layer's, which is where it is done.
/// </remarks>
public partial class ControlLinksView : UserControl
{
    /// <summary>Builds the list. Everything on it comes from the layer through bindings.</summary>
    public ControlLinksView()
    {
        InitializeComponent();
    }

    /// <summary>The layer this is showing, or nothing before it has been given one.</summary>
    private ControlLinksViewModel? Layer => DataContext as ControlLinksViewModel;

    /// <summary>
    /// Writes one controller's links on one target out as a template.
    /// </summary>
    /// <remarks>
    /// Somewhere of the person's choosing, opening in the templates folder, because a template
    /// is the copy that leaves: it is either kept where the program will find it or sent to
    /// somebody who has none of your links.
    /// </remarks>
    /// <param name="sender">The button, whose data is the controller's links on one target.</param>
    /// <param name="e">Unused.</param>
    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (Layer is not { } layer) return;
        if (sender is not Control { DataContext: ControllerLinks which }) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export template",
            SuggestedFileName = layer.Suggest(which),
            DefaultExtension = ControlTemplates.Extension,
            SuggestedStartLocation = await Folder(storage, layer),
            FileTypeChoices = new[] { OnDisc }
        });

        if (file?.TryGetLocalPath() is { } path) layer.Export(which, path);
    }

    /// <summary>Reads a template and lays its links down in this layer.</summary>
    /// <param name="sender">Unused.</param>
    /// <param name="e">Unused.</param>
    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (Layer is not { } layer) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import template",
            AllowMultiple = false,
            SuggestedStartLocation = await Folder(storage, layer),
            FileTypeFilter = new[] { OnDisc }
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) layer.Import(path);
    }

    /// <summary>
    /// The templates folder, for the picker to open in, or nothing if it cannot be reached.
    /// </summary>
    /// <remarks>
    /// A suggestion and never a restriction: a template can be written anywhere and opened from
    /// anywhere, since the point of one is that it travels. Nothing rather than an error where
    /// the folder cannot be made, which leaves the picker wherever it would have opened.
    /// </remarks>
    /// <param name="storage">The window's own file access.</param>
    /// <param name="layer">The layer, which knows where templates are kept.</param>
    private static async System.Threading.Tasks.Task<IStorageFolder?> Folder(IStorageProvider storage, ControlLinksViewModel layer)
    {
        try
        {
            return await storage.TryGetFolderFromPathAsync(layer.Folder());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>What a template looks like on disc.</summary>
    /// <remarks>Not called Template, which is a control's own property and would be hidden by it.</remarks>
    private static readonly FilePickerFileType OnDisc = new("Control template")
    {
        Patterns = new[] { "*." + ControlTemplates.Extension }
    };
}
