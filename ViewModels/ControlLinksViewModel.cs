using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Midi;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// What your controller is pointed at, as a list to read and to clear.
/// </summary>
/// <remarks>
/// Links are made by pointing rather than here: turn the mode on with Ctrl+Shift+M, rest the
/// pointer on a control, touch the one on the desk. This is the other half of that, and the
/// half a list is actually good for: seeing what you did, finding the one that is wrong, and
/// taking it off.
/// </remarks>
public sealed class ControlLinksViewModel : ObservableObject
{
    private readonly ControlLink _link;

    public ControlLinksViewModel(ControlLink link)
    {
        _link = link;

        _link.Changed += Restock;

        Restock();
    }

    public ObservableCollection<ControlLinkRow> Links { get; } = new();

    public bool HasLinks => Links.Count > 0;

    /// <summary>Takes every one off at once, for a controller being started again from nothing.</summary>
    public IRelayCommand ForgetAllCommand => new RelayCommand(() =>
    {
        foreach (var row in Links.ToList()) _link.Unlink(row.Mapping);
    });

    private void Restock()
    {
        Links.Clear();

        foreach (var mapping in _link.Mappings.OrderBy(one => one.Channel).ThenBy(one => one.Cc))
            Links.Add(new ControlLinkRow(mapping, _link));

        OnPropertyChanged(nameof(HasLinks));
    }
}

/// <summary>One line of it: which control, what it moves, and how it picks it up.</summary>
public sealed class ControlLinkRow
{
    private readonly ControlLink _link;

    public ControlLinkRow(ControlMapping mapping, ControlLink link)
    {
        Mapping = mapping;
        _link = link;
    }

    public ControlMapping Mapping { get; }

    /// <summary>The hardware, as the controller's own manual would put it.</summary>
    public string Control =>
        "CC " + Mapping.Cc.ToString(CultureInfo.InvariantCulture)
        + "  ch " + Mapping.Channel.ToString(CultureInfo.InvariantCulture);

    /// <summary>What it moves, named when it was learned.</summary>
    public string What => Mapping.Name.Length > 0 ? Mapping.Name : Mapping.Key;

    /// <summary>Which track, said only when it is pinned to one.</summary>
    public string Where => Mapping.Scope == ControlScope.Fixed
        ? "TR-" + (Mapping.Track + 1).ToString("00", CultureInfo.InvariantCulture)
        : "the track you are on";

    /// <summary>What kind of control it turned out to be, or that it is still listening.</summary>
    public string How => ControlSense.Describe(Mapping.Pickup, Mapping.Turn);

    /// <summary>
    /// Says it is something else, for the times the guess was wrong.
    /// </summary>
    /// <remarks>
    /// Working out what a control is from what it sends is right almost always and cannot be
    /// right every time: a button that repeats while it is held looks exactly like an encoder
    /// counting notches, because that is what both of them send. So the answer is a control you
    /// can press, and pressing past the end of the list puts it back to listening, which is how
    /// you ask it to work it out again from your next turn of the knob.
    /// </remarks>
    public IRelayCommand NextCommand => new RelayCommand(() =>
    {
        Mapping.Pickup = Mapping.Pickup switch
        {
            ControlPickup.Takeover => ControlPickup.Jump,
            ControlPickup.Jump => ControlPickup.Relative,
            ControlPickup.Relative when Mapping.Turn == ControlTurn.Offset => Turned(ControlTurn.Twos),
            ControlPickup.Relative => ControlPickup.Sensed,
            _ => ControlPickup.Takeover
        };

        _link.Say();
    });

    /// <summary>The other encoder convention, staying an encoder.</summary>
    private ControlPickup Turned(ControlTurn turn)
    {
        Mapping.Turn = turn;

        return ControlPickup.Relative;
    }

    public IRelayCommand ForgetCommand => new RelayCommand(() => _link.Unlink(Mapping));
}
