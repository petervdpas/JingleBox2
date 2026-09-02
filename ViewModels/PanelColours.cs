using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Views;
using System;
using System.Runtime.CompilerServices;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Views.Records;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// A machine's colours while somebody is choosing them.
/// </summary>
/// <remarks>
/// Its own copy of the theme, not the machine's. Colours are chosen by trying them, and trying
/// one means seeing it: a dialog that wrote straight onto the machine would leave the machine
/// wearing whatever was showing when Cancel was pressed.
///
/// The eight are one colour and seven distances from it, so the colour is the thing on the
/// picture and the distances are numbers underneath. Most machines never touch the seven, which
/// is why they are behind a dialog rather than on the page: a machine that wants a lighter face
/// or a louder mark says so here, and only here.
/// </remarks>
public sealed class PanelColours : ObservableObject
{
    /// <summary>A machine's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IPanelTint _tint = new PanelTint();

    /// <summary>Takes a copy of the machine's theme to work on.</summary>
    /// <param name="name">The machine's name, or a stand-in when it has not been given one yet.</param>
    /// <param name="theme">What it is wearing now, which is where the dialog starts.</param>
    public PanelColours(string name, PanelTheme theme)
    {
        Name = name.Length > 0 ? name : "The machine";

        _accent = _tint.Hue(theme.Accent, out var hue) ? _tint.Hex(hue) : Bare;

        _face = theme.Face;
        _panel = theme.Panel;
        _edge = theme.Edge;
        _mark = theme.Mark;
        _row = theme.Row;
        _rowOver = theme.RowOver;
        _rowPicked = theme.RowPicked;
    }

    /// <summary>The grey a machine wears until it is given a colour of its own.</summary>
    private const string Bare = "#7B838C";

    /// <summary>What the machine is called, so the preview looks like the thing being changed.</summary>
    public string Name { get; }

    /// <summary>What has been chosen, ready to be put on the machine.</summary>
    public PanelTheme Theme =>
        new(AccentHex, Face, Panel, Edge, Mark, Row, RowOver, RowPicked);

    /// <summary>The colour itself, kept as text because that is how a theme is written down.</summary>
    private string _accent;

    /// <summary>
    /// The colour written down, for somebody who has the number already.
    /// </summary>
    /// <remarks>
    /// Anything that is not a colour is refused and the box put back to what it was showing,
    /// rather than left holding half a number that the machine is not wearing. Said even when
    /// nothing changed, which is what makes the box snap back.
    /// </remarks>
    public string AccentHex
    {
        get => _accent;
        set
        {
            string wanted = _tint.Hue((value ?? "").Trim(), out var hue)
                ? _tint.Hex(hue)
                : _accent;

            if (wanted != _accent)
            {
                _accent = wanted;

                OnPropertyChanged(nameof(Accent));

                Repaint();
            }

            OnPropertyChanged();
        }
    }

    /// <summary>The same colour, as the picker deals in it.</summary>
    public Color Accent
    {
        get => _tint.Hue(_accent, out var hue) ? hue : Colors.Gray;
        set => AccentHex = _tint.Hex(value);
    }

    /// <summary>
    /// The seven distances from the colour, each nought to one.
    /// </summary>
    /// <remarks>
    /// Kept as plain fields with hand-written properties rather than generated ones, because every
    /// one of them is clamped and every one of them has to repaint the preview: the toolkit's
    /// generated setter would do neither.
    /// </remarks>
    private double _face;

    /// <inheritdoc cref="_face"/>
    private double _panel;

    /// <inheritdoc cref="_face"/>
    private double _edge;

    /// <inheritdoc cref="_face"/>
    private double _mark;

    /// <inheritdoc cref="_face"/>
    private double _row;

    /// <inheritdoc cref="_face"/>
    private double _rowOver;

    /// <inheritdoc cref="_face"/>
    private double _rowPicked;

    /// <summary>How far the chassis is darkened from the colour.</summary>
    public double Face { get => _face; set => Set(ref _face, value); }

    /// <summary>And the groups standing on it.</summary>
    public double Panel { get => _panel; set => Set(ref _panel, value); }

    /// <summary>How far the lines around them are lightened from it.</summary>
    public double Edge { get => _edge; set => Set(ref _edge, value); }

    /// <summary>And the marks, curves and meters.</summary>
    public double Mark { get => _mark; set => Set(ref _mark, value); }

    /// <summary>How much of the colour a row on a list is washed with.</summary>
    public double Row { get => _row; set => Set(ref _row, value); }

    /// <summary>The same row under the pointer.</summary>
    public double RowOver { get => _rowOver; set => Set(ref _rowOver, value); }

    /// <summary>And the row in hand.</summary>
    public double RowPicked { get => _rowPicked; set => Set(ref _rowPicked, value); }

    /// <summary>
    /// Puts one of the seven distances where it was asked to go, kept inside its range.
    /// </summary>
    /// <remarks>
    /// A move smaller than half a thousandth is dropped, since a slider dragged across the dialog
    /// reports hundreds of positions and a repaint per one of them would redraw the preview far
    /// more often than anybody can see.
    /// </remarks>
    private void Set(ref double held, double wanted, [CallerMemberName] string? name = null)
    {
        double kept = Math.Clamp(wanted, 0, 1);

        if (Math.Abs(kept - held) < 0.0005) return;

        held = kept;

        OnPropertyChanged(name);

        Repaint();
    }

    /// <summary>What the eight come to, for the preview to be painted from.</summary>
    private PanelShades Shades => _tint.Shades(Theme) ?? default;

    /// <summary>The chassis, as the preview paints it.</summary>
    public IBrush FaceBrush => new SolidColorBrush(Shades.Face);

    /// <summary>The groups standing on the chassis.</summary>
    public IBrush PanelBrush => new SolidColorBrush(Shades.Panel);

    /// <summary>The lines around them.</summary>
    public IBrush EdgeBrush => new SolidColorBrush(Shades.Edge);

    /// <summary>The marks, curves and meters.</summary>
    public IBrush MarkBrush => new SolidColorBrush(Shades.Mark);

    /// <summary>What is written on the face.</summary>
    public IBrush InkBrush => new SolidColorBrush(Shades.Ink);

    /// <summary>And what is written on it quietly.</summary>
    public IBrush MutedBrush => new SolidColorBrush(Shades.Muted);

    /// <summary>The three washes a list row takes, over whatever the list is standing on.</summary>
    public IBrush RowBrush => Wash(Row);

    /// <inheritdoc cref="RowBrush"/>
    public IBrush RowOverBrush => Wash(RowOver);

    /// <inheritdoc cref="RowBrush"/>
    public IBrush RowPickedBrush => Wash(RowPicked);

    /// <summary>The colour at the given strength, laid over whatever is behind the list.</summary>
    private IBrush Wash(double amount) => new SolidColorBrush(Accent, amount);

    /// <summary>Told when one of the eight moved, so the preview shows what it did.</summary>
    public event Action? Changed;

    /// <summary>
    /// Says every painted property changed at once, then tells whoever is watching.
    /// </summary>
    /// <remarks>
    /// The nine brushes are all worked out from the eight numbers, so any one of the eight moving
    /// changes all nine. Naming them one by one from each setter would mean a list per setter, and
    /// the day a shade was added one of the eight lists would be forgotten.
    /// </remarks>
    private void Repaint()
    {
        OnPropertyChanged(nameof(FaceBrush));
        OnPropertyChanged(nameof(PanelBrush));
        OnPropertyChanged(nameof(EdgeBrush));
        OnPropertyChanged(nameof(MarkBrush));
        OnPropertyChanged(nameof(InkBrush));
        OnPropertyChanged(nameof(MutedBrush));
        OnPropertyChanged(nameof(RowBrush));
        OnPropertyChanged(nameof(RowOverBrush));
        OnPropertyChanged(nameof(RowPickedBrush));

        Changed?.Invoke();
    }
}
