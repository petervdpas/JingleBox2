using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Machines;
using JingleBox2.Views;
using System;
using System.Runtime.CompilerServices;

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
public sealed class MachineColours : ObservableObject
{
    public MachineColours(string name, MachineTheme theme)
    {
        Name = name.Length > 0 ? name : "The machine";

        _accent = MachineTint.Hue(theme.Accent, out var hue) ? MachineTint.Hex(hue) : Bare;

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
    public MachineTheme Theme =>
        new(AccentHex, Face, Panel, Edge, Mark, Row, RowOver, RowPicked);

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
            string wanted = MachineTint.Hue((value ?? "").Trim(), out var hue)
                ? MachineTint.Hex(hue)
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
        get => MachineTint.Hue(_accent, out var hue) ? hue : Colors.Gray;
        set => AccentHex = MachineTint.Hex(value);
    }

    private double _face;
    private double _panel;
    private double _edge;
    private double _mark;
    private double _row;
    private double _rowOver;
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

    private void Set(ref double held, double wanted, [CallerMemberName] string? name = null)
    {
        double kept = Math.Clamp(wanted, 0, 1);

        if (Math.Abs(kept - held) < 0.0005) return;

        held = kept;

        OnPropertyChanged(name);

        Repaint();
    }

    /// <summary>What the eight come to, for the preview to be painted from.</summary>
    private MachineShades Shades => MachineTint.Shades(Theme) ?? default;

    public IBrush FaceBrush => new SolidColorBrush(Shades.Face);
    public IBrush PanelBrush => new SolidColorBrush(Shades.Panel);
    public IBrush EdgeBrush => new SolidColorBrush(Shades.Edge);
    public IBrush MarkBrush => new SolidColorBrush(Shades.Mark);
    public IBrush InkBrush => new SolidColorBrush(Shades.Ink);
    public IBrush MutedBrush => new SolidColorBrush(Shades.Muted);

    /// <summary>The three washes a list row takes, over whatever the list is standing on.</summary>
    public IBrush RowBrush => Wash(Row);
    public IBrush RowOverBrush => Wash(RowOver);
    public IBrush RowPickedBrush => Wash(RowPicked);

    private IBrush Wash(double amount) => new SolidColorBrush(Accent, amount);

    /// <summary>Told when one of the eight moved, so the preview shows what it did.</summary>
    public event Action? Changed;

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
