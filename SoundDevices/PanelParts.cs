using System.Collections.Generic;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.SoundDevices.Interfaces;

namespace JingleBox2.SoundDevices;

/// <inheritdoc/>
public sealed class PanelParts : IPanelParts
{
    /// <inheritdoc/>
    public IReadOnlyList<string> All { get; } = new[]
    {
        ElementKinds.Grid,
        ElementKinds.Group,
        ElementKinds.Row,
        ElementKinds.Column,
        ElementKinds.Strip,
        ElementKinds.Knob,
        ElementKinds.Fader,
        ElementKinds.Switch,
        ElementKinds.Number,
        ElementKinds.Button,
        ElementKinds.Choice,
        ElementKinds.Led,
        ElementKinds.Meter,
        ElementKinds.Keys,
        ElementKinds.Location,
        ElementKinds.Wave,
        ElementKinds.Envelope,
        ElementKinds.Scope,
        ElementKinds.Image,
        ElementKinds.Take,
        ElementKinds.Preset,
        ElementKinds.Pads,
        ElementKinds.Pad,
        ElementKinds.PadPicker,
        ElementKinds.Zones,
        ElementKinds.ZonePicker,
        ElementKinds.Slices,
        ElementKinds.Menu,
        ElementKinds.InstrumentName,
        ElementKinds.Label,
        ElementKinds.Text,
        ElementKinds.Spacer
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Each of these needs notes or a kit behind it, which is the one thing a box handed a
    /// track's audio has not got. A Take, a Wave, a Location, a Scope and a Preset are
    /// deliberately absent: a convolution reverb picks an impulse response off your shelf and
    /// draws it, a compressor traces its gain reduction, a delay on a track has a playhead, and
    /// every delay ever built ships presets. Those are unwired for an effect rather than wrong
    /// for one, and refusing them here would write this application's gaps into what an effect
    /// is allowed to be.
    /// </remarks>
    public IReadOnlyList<string> NeedNotes { get; } = new[]
    {
        ElementKinds.Keys,
        ElementKinds.Pads,
        ElementKinds.Pad,
        ElementKinds.PadPicker,
        ElementKinds.Zones,
        ElementKinds.ZonePicker,
        ElementKinds.Slices,
        ElementKinds.InstrumentName
    };

    /// <inheritdoc/>
    public IReadOnlyList<string> For(bool played) =>
        played ? All : All.Where(one => !NeedNotes.Contains(one)).ToArray();
}
