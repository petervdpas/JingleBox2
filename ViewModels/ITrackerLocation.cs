using System.ComponentModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// Where the tracker has got to, for a panel that wants to show it.
/// </summary>
/// <remarks>
/// An instrument's front panel opened from a track can say where that track is, the way the
/// Mother-32's OCTAVE / LOCATION lamps do. It needs two numbers to do that and nothing else,
/// so it asks for two numbers rather than for the tracker: the library page has no tracker
/// behind it at all, and the panel is the same panel in both places.
/// </remarks>
public interface ITrackerLocation : INotifyPropertyChanged
{
    /// <summary>The row being played, or -1 when nothing is playing.</summary>
    int PlayingLine { get; }

    /// <summary>How many rows the pattern has, which sets how many pages of eight there are.</summary>
    int PatternLines { get; }
}
