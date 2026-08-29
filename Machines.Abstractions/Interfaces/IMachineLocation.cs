using System;
using System.Collections.Generic;

namespace JingleBox2.Machines.Interfaces;

/// <summary>
/// Where the track playing this instrument has got to, for a machine that puts that on its face.
/// </summary>
/// <remarks>
/// It reports and does not sequence. The pattern belongs to the tracker and this only watches
/// it: the one thing the pages do is choose which run of rows the lamps are showing, and
/// pressing the page already shown hands that choice back to the playhead.
///
/// Not a setting, which is why it is a contract of its own rather than a parameter. Where a
/// song has got to is not a thing about the sound: two instruments off one machine do not
/// differ in it, and a song that remembered it would be a song remembering when you were
/// looking at it.
///
/// <see cref="Changed"/> for the reason <see cref="IMachineKeys"/> has one. A lamp moves on
/// every row the pattern plays, and that is not a redraw of the panel.
/// </remarks>
public interface IMachineLocation
{
    /// <summary>
    /// True when there is really a track behind this.
    /// </summary>
    /// <remarks>
    /// The rack edits an instrument nothing is playing. The row is drawn anyway and dimmed,
    /// because a panel that loses a part of itself depending on where it was opened is a
    /// different panel every time you look at it.
    /// </remarks>
    bool Live { get; }

    /// <summary>How many lamps one page shows.</summary>
    int Lamps { get; }

    /// <summary>Which lamp is lit, counted within the page on show. Nothing is lit at -1.</summary>
    int Lit { get; }

    /// <summary>The row number written under the first lamp.</summary>
    int FirstNumber { get; }

    /// <summary>What is written on each page's button: the rows that page covers.</summary>
    IReadOnlyList<string> Pages { get; }

    /// <summary>Which page is on show.</summary>
    int Page { get; }

    /// <summary>Shows that page, or lets go again when it is the one already shown.</summary>
    void Show(int page);

    /// <summary>Told when the playhead moved, or the pattern changed length.</summary>
    event EventHandler? Changed;
}
