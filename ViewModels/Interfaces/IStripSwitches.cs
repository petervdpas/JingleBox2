namespace JingleBox2.ViewModels.Interfaces;

/// <summary>The two switches every strip in this application has: silence it, or hear only it.</summary>
/// <remarks>
/// **One contract because there are two kinds of strip and the switches are the same switches.**
/// A track's strip and a strip over something that is not a track are different classes for good
/// reasons, and to a hand reaching for M or S they are one thing. Without this, anything wanting
/// to offer those two would have to know which of the two it was holding, and the patchbay's
/// sidebar holds both.
///
/// Whether each may be pressed at all is part of it, since a strip that cannot solo is an
/// ordinary state rather than a fault: the master cannot, because soloing everything is what it
/// is already doing, and a source with no bus under it can be neither muted nor soloed.
/// A switch that cannot be pressed is drawn dark rather than taken away, which is the rule the
/// mixer's own strips already keep: a control that vanishes takes the layout with it.
/// </remarks>
public interface IStripSwitches
{
    /// <summary>Whether this can be silenced at all.</summary>
    bool CanMute { get; }

    /// <summary>Whether it can be soloed at all.</summary>
    bool CanSolo { get; }

    /// <summary>Whether it is silenced, with its fader left where it stands.</summary>
    bool Mute { get; set; }

    /// <summary>Whether it is the only thing being heard.</summary>
    bool Solo { get; set; }
}
