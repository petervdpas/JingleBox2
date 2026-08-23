using Avalonia;
using Avalonia.Controls;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The recording a machine is holding, cut into pieces: the picture, the boundaries, and what
/// it takes to make more or fewer of them.
/// </summary>
/// <remarks>
/// It has no way of loading anything, on purpose. The recording is whichever one the machine
/// already plays, so there is one place to put a sample and this is not it. What a piece
/// becomes afterwards, a stretch of keyboard on Zampler or one key on BongaBong, is settled by
/// whoever made the view model, so nothing here has to know which machine it is sitting on.
/// </remarks>
public partial class SliceEditor : UserControl
{
    /// <summary>The recording being cut and where its boundaries are.</summary>
    public static readonly StyledProperty<SliceEditorViewModel?> SlicesProperty =
        AvaloniaProperty.Register<SliceEditor, SliceEditorViewModel?>(nameof(Slices));

    public SliceEditor()
    {
        InitializeComponent();

        // Given its own context rather than the panel's, so the panel can hand it in as one
        // property among the others it sets from its own data.
        Body.Bind(DataContextProperty, this.GetObservable(SlicesProperty));
    }

    public SliceEditorViewModel? Slices
    {
        get => GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }
}
