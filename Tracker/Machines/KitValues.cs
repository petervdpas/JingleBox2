using JingleBox2.Machines;
using JingleBox2.ViewModels;
using System;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// BongaBong's panel, wired to a real kit.
/// </summary>
/// <remarks>
/// What <see cref="RecordingValues"/> is for the machine that plays one recording, this is for
/// the machine that plays sixteen. The difference is what a key means. On a machine holding one
/// sound, "level" is the sound's level and there is nothing else it could be about. On a kit
/// there are sixteen levels and the panel shows one knob, so every key here is about the pad in
/// hand: press a different pad and the same knob is about a different drum.
///
/// That is the machine, not a shortcut. A drum machine has always had one strip of controls and
/// a grid of pads to point it at, because sixteen strips would not fit on any panel anybody
/// could reach across. The pad in hand is where somebody is looking, which is why it lives with
/// the pads (<see cref="KitPads"/>) and not in the song.
///
/// A key it does not know reads as zero and swallows the write, for the reason the recording
/// machine's does: a machine.json written by a later version has to open on an older app rather
/// than take it down.
/// </remarks>
/// <param name="kit">The kit these settings are on.</param>
/// <param name="about">
/// Which pad, or nothing for whichever is in hand. The panel wants the pad in hand, because
/// that is what a front panel shows; a preset wants a named one, because a preset holds all
/// sixteen. Both are the same mapping from a key to a thing on a pad, so they are the same
/// class asked about a different pad.
/// </param>
public sealed class KitValues(DrumKitViewModel kit, Func<DrumPadViewModel?>? about = null) : MachineValues
{
    /// <summary>How loud the pad in hand is.</summary>
    /// <remarks>
    /// The keys are written out one by one, never built from a name or a loop, so every key in
    /// the application can be found by searching for the string that is in the machine's own
    /// file. A key assembled at the call site never appears in the source at all, and the tools
    /// that look for an orphaned key, and anybody grepping, both miss it.
    /// </remarks>
    private const string LevelKey = "pad_level";

    /// <summary>Where it sits across the stereo picture.</summary>
    private const string PanKey = "pad_pan";

    /// <summary>Which choke group it is in, so a hi-hat can cut its own open sound.</summary>
    private const string ChokeKey = "pad_choke";

    /// <summary>The recording on the pad in hand, which the Take control puts there.</summary>
    private const string TakeKey = "pad_take";

    /// <summary>What that pad is called, which is yours to type.</summary>
    private const string NameKey = "pad_name";

    /// <summary>And the file it is playing, said in one line under the name.</summary>
    private const string DetailsKey = "pad_details";

    /// <summary>The pad every one of these keys is about, or nothing before one is picked.</summary>
    private DrumPadViewModel? Pad => about != null ? about() : kit.Selected;

    /// <inheritdoc/>
    /// <remarks>
    /// A key it does not know reads as nought rather than throwing, and with no pad in hand
    /// every key reads as the setting's own resting value.
    /// </remarks>
    public override double Get(string key) => key switch
    {
        LevelKey => Pad?.Volume ?? 1,
        PanKey => Pad?.Pan ?? 0,
        ChokeKey => Pad?.Choke ?? 0,
        _ => 0,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing at all is written with no pad in hand. A panel can be drawn before a pad has been
    /// pressed, and a knob moved then would otherwise have to pick a drum to be about.
    /// </remarks>
    protected override bool Write(string key, double value)
    {
        if (Pad is not { } pad) return false;

        return key switch
        {
            LevelKey => Moved(pad.Volume, value, () => pad.Volume = value),
            PanKey => Moved(pad.Pan, value, () => pad.Pan = value),
            ChokeKey => Moved(pad.Choke, Math.Round(value), () => pad.Choke = Math.Round(value)),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public override string GetText(string key) => key switch
    {
        TakeKey => Pad?.Pad.FilePath ?? "",
        NameKey => Pad?.Name ?? "",
        DetailsKey => Pad?.FileText ?? "",
        _ => "",
    };

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="DetailsKey"/> is read only: it is the file the pad is playing, said in one
    /// line, and the way to change it is to put a different recording on the pad.
    ///
    /// A take goes on through the pad's own way of taking one, which names the pad after the
    /// file when it has no name of its own. A pad called "live-snare-shot-pic" is a pad you can
    /// find in the grid; a pad called nothing is sixteen pads called nothing.
    /// </remarks>
    protected override bool WriteText(string key, string value)
    {
        if (Pad is not { } pad) return false;

        switch (key)
        {
            case TakeKey:
                if (FilePaths.Same(pad.Pad.FilePath, value)) return false;

                pad.Take(value);

                return true;

            case NameKey:
                if (pad.Name == value) return false;

                pad.Name = value;

                return true;

            default:
                return false;
        }
    }

}
