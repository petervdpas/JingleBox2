using System;
using System.Collections.Generic;

namespace JingleBox2.Rack.SoundDevices.Faces.Interfaces;

/// <summary>
/// Where a sound device can be started from.
/// </summary>
/// <remarks>
/// A panel opens with the same question wherever it is standing: which of these do you want to
/// begin with. Usually the answer is one of the presets in the box's own folder; on the
/// Recording soundmachine it is one of your takes instead, because a box whose whole sound is
/// the recording on it has no settings worth shipping. The panel does not care which, so it is
/// handed a list of names and told what the box calls the list.
///
/// Names and a number rather than a list of presets, for the same reason
/// <see cref="IPanelTakes"/> hands over peaks rather than a recording. What a preset is, where
/// it was read from and what happens to the settings when one is picked all belong to whoever
/// owns the shelf. The panel's whole part in it is drawing the name that is showing and saying
/// when somebody asked for a different one.
///
/// Picking is <see cref="Picked"/> rather than an event, unlike the recording a
/// <see cref="IPanelTakes"/> panel asks for, because the list is already here. There is
/// nothing for the host to go and fetch: the panel can say which one, and which one is a number.
/// </remarks>
public interface IPanelPresets
{
    /// <summary>What is offered, in the order it is offered.</summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>Which one is showing, or -1 for none.</summary>
    /// <remarks>
    /// Written when somebody works the picker, which is the whole of what the panel does to
    /// this. Whether that loads anything is the implementer's business.
    /// </remarks>
    int Picked { get; set; }

    /// <summary>What this sound device calls the list: "Preset", or "Take".</summary>
    string Caption { get; }

    /// <summary>
    /// The ways the list can be narrowed, or none when there is nothing to narrow.
    /// </summary>
    /// <remarks>
    /// A shelf of your own recordings runs to hundreds and is filed under categories, and
    /// hunting through it with two arrows is no way to find anything. A sound device's own presets
    /// are a handful shipped in a folder and have nothing to file, which is why this is allowed
    /// to be empty and the picker keeps the whole width when it is.
    ///
    /// Given a body so that a shelf that has never heard of narrowing still compiles. What the
    /// categories are, and whether one of them is "everything", is the shelf's own business:
    /// the panel draws the words it is given and hands back the one that was picked.
    /// </remarks>
    IReadOnlyList<string> Filters => Array.Empty<string>();

    /// <summary>Which of those is in force, by name.</summary>
    string Filter { get => ""; set { } }
}
