using System.Collections.Generic;

namespace JingleBox2.SoundDevices.Interfaces;

/// <summary>
/// Which parts the designer can put on a face, and which of them only a played box can fill.
/// </summary>
/// <remarks>
/// A part on a face is a control plus whatever the host has to supply behind it. A Knob needs
/// somewhere to read and write a number, which any box has; a Keys needs
/// <c>JingleBox2.Rack.SoundDevices.Faces.Interfaces.IPanelKeys</c>, which is a keyboard, and a
/// box that is handed a track's audio has none. Dropping one of those on an effect's face draws
/// a control that nothing will ever fill, and nothing anywhere says why, which reads as the
/// designer being broken rather than as the part being wrong for the box.
///
/// So the list is a fact about the world being designed and is asked of it through
/// <c>JingleBox2.SoundDevices.Interfaces.IDesignWorld.Parts</c>. Here so that both answers are
/// in one place: two lists written out separately would drift the first time a part is added,
/// and the way that fails is a part that quietly cannot be placed on either.
///
/// Written out rather than worked out from the constants, so the order is the one that suits
/// somebody building a panel, containers first and controls after, and so a constant added for
/// a control the designer cannot yet place does not turn up in the list on its own.
/// </remarks>
public interface IPanelParts
{
    /// <summary>Every part the designer can place, in the order a panel is built in.</summary>
    IReadOnlyList<string> All { get; }

    /// <summary>
    /// The parts that need notes or a kit behind them: a keyboard, a kit, a keyboard map, a
    /// chopped recording and the instrument's name badge.
    /// </summary>
    /// <remarks>
    /// A Scope, a Preset, a Take, a Wave and a Location are deliberately not here. A compressor
    /// drawing its gain reduction, a convolution reverb picking an impulse response off your
    /// shelf and a delay shipping presets are all ordinary, so those belong on any face and are
    /// only unwired for an effect. Refusing them would write this application's gaps into what an
    /// effect is allowed to be.
    /// </remarks>
    IReadOnlyList<string> NeedNotes { get; }

    /// <summary>What a face may carry, given whether the box in it is played.</summary>
    /// <param name="played">True for a box that is sent notes.</param>
    IReadOnlyList<string> For(bool played);
}
