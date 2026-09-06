using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.UI.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// One of a block's outputs in the sidebar: what it is called, what it carries, and the two
/// switches that belong to it.
/// </summary>
/// <remarks>
/// **One row shape for every block, which is what makes the sidebar readable.** A track, the
/// pads, a take and the master are four different things in this application and to a hand
/// reaching for M or S they are one, so the row is drawn once and what differs is only whether
/// the switches can be pressed. That is the mixer's own rule said again: a switch that cannot
/// be pressed is dark rather than gone, since a control that vanishes takes the layout with it.
///
/// It holds a strip rather than copying its state, so pressing M here and pressing M on the
/// desk are the same press on the same thing. A row over nothing is an ordinary row with both
/// switches dark, which is what a block on the machine gets: somebody else's program has no
/// mute of ours.
/// </remarks>
public sealed class PatchSwitchViewModel : ObservableObject
{
    /// <summary>The strip this row drives, or nothing where there is none.</summary>
    private readonly IStripSwitches? _strip;

    /// <summary>Takes one output and whatever strip answers for it.</summary>
    /// <param name="port">The output, as the block draws it.</param>
    /// <param name="strip">What its switches reach, or nothing where nothing does.</param>
    public PatchSwitchViewModel(PatchPort port, IStripSwitches? strip)
    {
        Name = port.Name;
        Shape = port.Shape;

        _strip = strip;
    }

    /// <summary>What the output is called on the face of the block.</summary>
    public string Name { get; }

    /// <summary>Mono or stereo, in the word the sidebar shows.</summary>
    public string Shape { get; }

    /// <summary>Whether the mute can be pressed at all.</summary>
    public bool CanMute => _strip?.CanMute ?? false;

    /// <summary>Whether the solo can.</summary>
    public bool CanSolo => _strip?.CanSolo ?? false;

    /// <summary>Whether this output is silenced.</summary>
    public bool Mute
    {
        get => _strip?.Mute ?? false;
        set
        {
            if (_strip == null || _strip.Mute == value) return;

            _strip.Mute = value;

            OnPropertyChanged();
        }
    }

    /// <summary>Whether this output is the only thing being heard.</summary>
    public bool Solo
    {
        get => _strip?.Solo ?? false;
        set
        {
            if (_strip == null || _strip.Solo == value) return;

            _strip.Solo = value;

            OnPropertyChanged();
        }
    }
}
