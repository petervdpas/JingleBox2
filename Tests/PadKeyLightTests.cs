using JingleBox2.Rack.Machines.Interfaces;
using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Machines;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A pad held down lights its own key, the same way that key held down does.
/// </summary>
/// <remarks>
/// A pad is a key with two halves like any other, and the contract had only the one for a long
/// time: the moment it sounds. So a pad hit lit the pad and left the drawn keyboard dark, while
/// clicking the very same note on that keyboard lit it. They are one act and look like one now.
/// </remarks>
public class PadKeyLightTests
{
    private static (IMachinePads Pads, IMidiMonitor Keys, DrumKit Kit) Kitted()
    {
        var kit = new DrumKit();

        for (int at = 0; at < 4; at++)
            kit.Pads.Add(new DrumPad { Semitone = 60 + at, Name = "Pad " + at });

        var keys = new MidiMonitor();
        var pads = new KitPads(new DrumKitViewModel(kit, () => { }, _ => { }), keys);

        return (pads, keys, kit);
    }

    /// <summary>A hand on a pad puts its key down, and taking it off puts the key up.</summary>
    [Fact]
    public void A_pad_held_lights_its_own_key()
    {
        var (pads, keys, _) = Kitted();

        pads.Held(0);

        Assert.True(keys.Holds(60));
        Assert.Equal(new[] { 60 }, keys.Down);

        pads.Let(0);

        Assert.False(keys.Holds(60));
        Assert.Empty(keys.Down);
    }

    /// <summary>Each pad lights the key it answers to, and no other.</summary>
    [Fact]
    public void Each_pad_lights_its_own_key()
    {
        var (pads, keys, _) = Kitted();

        pads.Held(2);

        Assert.True(keys.Holds(62));
        Assert.False(keys.Holds(60));
    }

    /// <summary>Two pads at once are two keys at once, as two fingers would be.</summary>
    [Fact]
    public void Two_pads_light_two_keys()
    {
        var (pads, keys, _) = Kitted();

        pads.Held(0);
        pads.Held(3);

        Assert.True(keys.Holds(60));
        Assert.True(keys.Holds(63));

        pads.Let(0);

        Assert.False(keys.Holds(60));
        Assert.True(keys.Holds(63));
    }

    /// <summary>
    /// A pad that is not there does nothing, rather than throwing on the drawing thread.
    /// </summary>
    /// <remarks>
    /// A machine's description says how many pads its grid has and the kit behind it may have
    /// fewer, which the grid already draws as empty pads. A hand going down on one of those
    /// must not take the panel with it.
    /// </remarks>
    [Fact]
    public void A_pad_that_is_not_there_does_nothing()
    {
        var (pads, keys, _) = Kitted();

        pads.Held(40);
        pads.Let(40);
        pads.Held(-1);
        pads.Let(-1);

        Assert.Empty(keys.Down);
    }

    /// <summary>Without a monitor a pad still works, and nothing lights.</summary>
    /// <remarks>
    /// That is what a preview has: no hand on it and no keyboard reading it.
    /// </remarks>
    [Fact]
    public void Without_a_monitor_nothing_lights()
    {
        var kit = new DrumKit();
        kit.Pads.Add(new DrumPad { Semitone = 60 });

        var pads = new KitPads(new DrumKitViewModel(kit, () => { }, _ => { }));

        pads.Held(0);
        pads.Let(0);
    }

    /// <summary>
    /// A pad with no playable note lights nothing.
    /// </summary>
    /// <remarks>
    /// A pad answers to a key, and a pad whose key is not one has nothing to light. Nought is a
    /// real note and is not the empty case, which is what makes this worth asking.
    /// </remarks>
    [Fact]
    public void A_pad_with_no_note_lights_nothing()
    {
        var kit = new DrumKit();
        kit.Pads.Add(new DrumPad { Semitone = -1 });

        var keys = new MidiMonitor();
        var pads = new KitPads(new DrumKitViewModel(kit, () => { }, _ => { }), keys);

        pads.Held(0);

        Assert.Empty(keys.Down);
    }
}
