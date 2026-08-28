using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace JingleBox2.Views;

/// <summary>
/// A picture of what is in the hand, following it across a page.
/// </summary>
/// <remarks>
/// Both places here that drag things want one and neither can have the toolkit's. The machine
/// designer follows the pointer itself and so was never offered one; the tracker uses the
/// toolkit's own drag and drop, which draws nothing at all on X11. So the picture is drawn
/// here, on a canvas laid over the page that takes no clicks.
///
/// Told what to draw rather than what is being carried, because a part of a machine and an
/// instrument going onto a track have nothing in common except that somebody is holding one.
///
/// Put down in the <c>finally</c> of the drag rather than on the drop, because a drag is just
/// as often abandoned: let go over the order list or off the window and no drop is ever raised,
/// and the picture would be left on the page with nothing under it.
/// </remarks>
public sealed class DragGhost
{
    /// <summary>
    /// Below and right of the hand, so what is being aimed at is not under the picture.
    /// </summary>
    private const double Offset = 12;

    /// <summary>How solid the picture is where it can land, and where it cannot.</summary>
    private const double Carried = 0.85;

    /// <summary>Fully solid where it cannot land, which is the one place it must not fade.</summary>
    private const double Refusing = 1.0;

    /// <summary>The canvas over the page, which takes no clicks so the drag still reaches it.</summary>
    private readonly Canvas _layer;

    /// <summary>The card on the layer, and null while the hand is empty.</summary>
    private Border? _shown;

    /// <inheritdoc cref="Refused"/>
    private bool _refused;

    /// <summary>Draws onto that layer, which is expected to be laid over the whole page.</summary>
    public DragGhost(Canvas layer) => _layer = layer;

    /// <summary>True once there is something in the hand, so a caller can show it only once.</summary>
    public bool IsShowing => _shown != null;

    /// <summary>
    /// Whether the hand is over somewhere this cannot land.
    /// </summary>
    /// <remarks>
    /// Said on the picture rather than left to the pointer, because the pointer's own answer is
    /// a cursor that swaps for a barred circle, and a picture that vanishes at the same moment
    /// reads as the drag having gone wrong rather than as the place being the wrong one. So the
    /// picture stays put and turns red, and gets more solid rather than less: the one thing it
    /// must not do where nothing will happen is fade away.
    /// </remarks>
    public bool Refused
    {
        get => _refused;
        set
        {
            if (_refused == value) return;

            _refused = value;

            Paint();
        }
    }

    /// <summary>
    /// Puts the card into the state <see cref="Refused"/> says it is in.
    /// </summary>
    /// <remarks>
    /// The ordinary look is cleared rather than set to null. The card is a style, and a local
    /// null is a value like any other: it would win, and the picture would lose the background
    /// the theme gave it and be a floating line of text over the pattern.
    ///
    /// Refused is a wash of the same red as the border and not only the border, so the card
    /// reads as red rather than as merely outlined in it.
    /// </remarks>
    private void Paint()
    {
        if (_shown == null) return;

        _shown.Opacity = _refused ? Refusing : Carried;

        if (!_refused)
        {
            _shown.ClearValue(Border.BorderBrushProperty);
            _shown.ClearValue(Border.BorderThicknessProperty);
            _shown.ClearValue(Border.BackgroundProperty);
            return;
        }

        var red = Red();

        _shown.BorderBrush = red;
        _shown.BorderThickness = new Thickness(2);

        _shown.Background = new SolidColorBrush(red.Color, 0.35);
    }

    /// <summary>
    /// The theme's own red, with one of its own if the theme cannot be reached.
    /// </summary>
    /// <remarks>
    /// The fallback is not tidiness. This colour is the whole of how the picture says nothing
    /// will happen here, and a lookup that quietly came back empty would leave a refused drop
    /// looking exactly like one that would work.
    /// </remarks>
    private ISolidColorBrush Red() =>
        _layer.TryFindResource("DangerBrush", out object? found) && found is ISolidColorBrush brush
            ? brush
            : new SolidColorBrush(Color.FromRgb(0xB6, 0x4A, 0x4A));

    /// <summary>Puts a picture in the hand, replacing whatever was there.</summary>
    public void Show(Control inside)
    {
        Hide();

        _shown = new Border
        {
            Classes = { "card" },
            Padding = new Thickness(6),
            Child = inside,
        };

        Paint();

        _layer.Children.Add(_shown);
    }

    /// <summary>Where the hand is, in the coordinates of the page the layer covers.</summary>
    public void MoveTo(Point at)
    {
        if (_shown == null) return;

        Canvas.SetLeft(_shown, at.X + Offset);
        Canvas.SetTop(_shown, at.Y + Offset);
    }

    /// <summary>
    /// Takes the picture off the page and empties the hand.
    /// </summary>
    /// <remarks>
    /// Safe to call with nothing showing, because it is called from the <c>finally</c> of a drag
    /// that may have been abandoned before anything was ever put in the hand.
    /// </remarks>
    public void Hide()
    {
        if (_shown == null) return;

        _layer.Children.Remove(_shown);

        _shown = null;
        _refused = false;
    }
}
