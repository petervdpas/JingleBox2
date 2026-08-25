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
/// Which pad, or nothing for whichever is in hand.
/// </param>
/// <remarks>
/// The panel wants the pad in hand, because that is what a front panel shows; a preset wants a
/// named one, because a preset holds all sixteen. Both are the same mapping from a key to a
/// thing on a pad, so they are the same class asked about a different pad.
/// </remarks>
public sealed class KitValues(DrumKitViewModel kit, Func<DrumPadViewModel?>? about = null) : IMachineValues
{
    // Written out one by one, never built from a name or a loop, so every key in the app can be
    // found by searching for the string that is in the file.
    private const string LevelKey = "pad_level";
    private const string PanKey = "pad_pan";
    private const string ChokeKey = "pad_choke";

    /// <summary>The recording on the pad in hand, which the Take control puts there.</summary>
    private const string TakeKey = "pad_take";

    /// <summary>What that pad is called, which is yours to type.</summary>
    private const string NameKey = "pad_name";

    /// <summary>And the file it is playing, said in one line under the name.</summary>
    private const string DetailsKey = "pad_details";

    /// <summary>Told when something moved, for saving the song and redrawing what else shows it.</summary>
    public Action? Changed { get; set; }

    /// <summary>The pad every one of these keys is about, or nothing before one is picked.</summary>
    private DrumPadViewModel? Pad => about != null ? about() : kit.Selected;

    public double Get(string key) => key switch
    {
        LevelKey => Pad?.Volume ?? 1,
        PanKey => Pad?.Pan ?? 0,
        ChokeKey => Pad?.Choke ?? 0,
        _ => 0,
    };

    public void Set(string key, double value)
    {
        if (Pad is not { } pad) return;

        bool moved = key switch
        {
            LevelKey => Moved(pad.Volume, value, () => pad.Volume = value),
            PanKey => Moved(pad.Pan, value, () => pad.Pan = value),
            ChokeKey => Moved(pad.Choke, Math.Round(value), () => pad.Choke = Math.Round(value)),
            _ => false,
        };

        if (moved) Changed?.Invoke();
    }

    public string GetText(string key) => key switch
    {
        TakeKey => Pad?.Pad.FilePath ?? "",
        NameKey => Pad?.Name ?? "",
        DetailsKey => Pad?.FileText ?? "",
        _ => "",
    };

    public void SetText(string key, string value)
    {
        if (Pad is not { } pad) return;

        switch (key)
        {
            case TakeKey:
                if (pad.Pad.FilePath == value) return;

                // Through the pad's own way of taking one, which names it after the file when
                // it has no name of its own. A pad called "live-snare-shot-pic" is a pad you can
                // find in the grid; a pad called nothing is sixteen pads called nothing.
                pad.Take(value);

                break;

            case NameKey:
                if (pad.Name == value) return;

                pad.Name = value;

                break;

            default:
                return;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Writes it if it really is different, and says whether it was.
    /// </summary>
    /// <remarks>
    /// A knob reports the value it already has on every mouse move that did not cross a step,
    /// and a song marked dirty by that is a song that can never be closed without being asked
    /// about.
    /// </remarks>
    private static bool Moved(double was, double now, Action write)
    {
        if (Math.Abs(was - now) < 1e-9) return false;

        write();

        return true;
    }
}
