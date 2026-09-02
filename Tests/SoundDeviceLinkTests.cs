using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A link on a device, which is one answer for a soundmachine and for an effect.
/// </summary>
/// <remarks>
/// The rack holds devices and a device is one or the other, so a knob pointed at one names the
/// device's id and the key of the control under the pointer. It was two answers for a while, and
/// the second one was wrong in a way nothing said out loud: an effect's link went in as an
/// <c>Insert</c>, which is the word for a plugin, and every link of that kind is thrown away as
/// the settings are read. The link was made, it worked until the app was closed, and it was gone
/// in the morning.
/// </remarks>
public class SoundDeviceLinkTests
{
    /// <summary>What a device's control offers. Holds nothing, so one serves every test here.</summary>
    private static readonly ISoundDeviceLinks Links = new SoundDeviceLinks();

    /// <summary>The words a link is cut into cards and files by.</summary>
    private static readonly ILinkTargets Targets = new LinkTargets();

    /// <summary>A machine and an effect are offered in exactly the same shape.</summary>
    /// <remarks>
    /// Which of the two it is decides where the link is looked for later and nothing about the
    /// link itself, which is the whole of what "uniform" means here.
    /// </remarks>
    [Fact]
    public void A_machine_and_an_effect_are_offered_the_same_way()
    {
        var machine = Links.On("machine.oddskilla", "OddSkilla", "cutoff");
        var effect = Links.On("effect.echobox", "EchoBox", "time");

        Assert.Equal(ControlKind.SoundDevice, machine.Kind);
        Assert.Equal(ControlKind.SoundDevice, effect.Kind);

        Assert.Equal(ControlScope.Focused, machine.Scope);
        Assert.Equal(ControlScope.Focused, effect.Scope);

        Assert.Equal("machine.oddskilla", machine.Machine);
        Assert.Equal("effect.echobox", effect.Machine);

        Assert.Equal("cutoff", machine.Key);
        Assert.Equal("time", effect.Key);

        Assert.Equal("OddSkilla", machine.Owner);
        Assert.Equal("EchoBox time", effect.Name);
    }

    /// <summary>A button is a press: it jumps, and its word reads as words.</summary>
    [Fact]
    public void A_button_jumps_and_says_what_it_does()
    {
        var made = Links.Action("machine.bongabong", "BongaBong", "load_pads");

        Assert.Equal(ControlKind.Action, made.Kind);
        Assert.Equal(ControlPickup.Jump, made.Pickup);
        Assert.Equal("BongaBong load pads", made.Name);
    }

    /// <summary>A device with no name of its own is still a link, and says only what it turns.</summary>
    [Fact]
    public void A_device_that_says_no_name_still_makes_a_link()
    {
        var made = Links.On("effect.echobox", "", "mix");

        Assert.Equal("mix", made.Name);
        Assert.Equal("", made.Owner);
    }

    /// <summary>
    /// An effect's link survives the settings being read, which is the fault this all began as.
    /// </summary>
    /// <remarks>
    /// The constructor throws away every plugin link, because a plugin cannot be pointed at. An
    /// effect of ours is not a plugin, and a link on one has to be there in the morning.
    /// </remarks>
    [Fact]
    public void An_effects_link_is_still_there_when_the_settings_are_read()
    {
        var kept = new List<ControlMapping>
        {
            Links.On("effect.echobox", "EchoBox", "time"),
            Links.On("machine.oddskilla", "OddSkilla", "cutoff"),
            new() { Kind = ControlKind.Plugin, Plugin = "com.somebody.thing", Parameter = 12 }
        };

        _ = new ControlLink(kept, () => { });

        Assert.Equal(2, kept.Count);
        Assert.Contains(kept, one => one.Machine == "effect.echobox");
        Assert.Contains(kept, one => one.Machine == "machine.oddskilla");
        Assert.DoesNotContain(kept, one => one.Kind == ControlKind.Plugin);
    }

    /// <summary>Both are cut into cards by the same rule, and by their own id.</summary>
    [Fact]
    public void Both_are_cut_into_cards_the_same_way()
    {
        var machine = Links.On("machine.oddskilla", "OddSkilla", "cutoff");
        var effect = Links.On("effect.echobox", "EchoBox", "time");

        Assert.Equal("sounddevice", Targets.KindOf(machine));
        Assert.Equal("sounddevice", Targets.KindOf(effect));

        Assert.Equal("machine.oddskilla", Targets.IdOf(machine));
        Assert.Equal("effect.echobox", Targets.IdOf(effect));

        Assert.Equal("cutoff", Targets.ParameterOf(machine));
        Assert.Equal("time", Targets.ParameterOf(effect));

        Assert.NotEqual(Targets.KeyOf(machine), Targets.KeyOf(effect));
        Assert.Equal(Targets.KeyOf(machine), Targets.KeyOf(Links.On("machine.oddskilla", "OddSkilla", "decay")));
    }

    /// <summary>A card is headed by the thing itself, whichever of the two it is.</summary>
    [Fact]
    public void A_card_is_headed_by_the_device()
    {
        Assert.Equal("EchoBox", Targets.TitleOf(new[] { Links.On("effect.echobox", "EchoBox", "time") }));
        Assert.Equal("OddSkilla", Targets.TitleOf(new[] { Links.On("machine.oddskilla", "OddSkilla", "cutoff") }));
    }

    /// <summary>
    /// A file says sounddevice, and it says it for both worlds.
    /// </summary>
    /// <remarks>
    /// What is refused is a plugin, under whichever word it arrives: a plugin brings its own
    /// MIDI learn and nothing can make that agree with a link made here. A word this version
    /// does not know is refused the same way, so the caller counts what it could not read
    /// rather than failing the whole file.
    /// </remarks>
    [Fact]
    public void A_file_says_sounddevice_for_both_worlds()
    {
        Assert.NotNull(Targets.Point("sounddevice", "effect.echobox", "time"));
        Assert.NotNull(Targets.Point("sounddevice", "machine.oddskilla", "cutoff"));

        Assert.Null(Targets.Point("plugin", "com.somebody.thing", "12"));
        Assert.Null(Targets.Point("sounddevice", "effect.echobox", ""));
        Assert.Null(Targets.Point("sounddevice", "", "time"));
        Assert.Null(Targets.Point("nonsense", "effect.echobox", "time"));
    }

    /// <summary>What a file says a device link is, read back as the link it was.</summary>
    [Fact]
    public void A_device_link_goes_out_and_comes_back()
    {
        var made = Links.On("effect.echobox", "EchoBox", "feedback");

        var read = Targets.Point(Targets.KindOf(made), Targets.IdOf(made), Targets.ParameterOf(made), "EchoBox");

        Assert.NotNull(read);
        Assert.Equal(made.Kind, read!.Kind);
        Assert.Equal(made.Machine, read.Machine);
        Assert.Equal(made.Key, read.Key);
        Assert.Equal("EchoBox", read.Owner);
    }
}
