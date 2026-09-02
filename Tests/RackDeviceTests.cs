using System;
using System.Collections.Generic;
using JingleBox2.Rack.Faces;
using JingleBox2.Devices.SoundEffects;
using JingleBox2.Devices.SoundMachines;
using Xunit;
using JingleBox2.Devices.Interfaces;

namespace JingleBox2.Tests;

/// <summary>
/// What this installation has, asked once for both kinds of device.
/// </summary>
/// <remarks>
/// The rack holds devices and a device is a soundmachine or an effect, so what is there, whether
/// an id is one of them, which one it is and what its face looks like are one question with one
/// answer. It was two lists with the same four members, and a question asked twice eventually
/// gets two answers.
///
/// Every test here is run against both worlds through the same interface, which is the point: if
/// the two ever stop agreeing, these stop passing.
/// </remarks>
public class RackDeviceTests
{
    /// <summary>A machine's list, filled with machines.</summary>
    private static IRackDevices<SoundMachineProject> Machines(params SoundMachineProject[] found)
    {
        var kept = new SoundMachineProjects();

        kept.Keep(found);

        return kept;
    }

    /// <summary>And an effect's, filled with effects, which is the same class underneath.</summary>
    private static IRackDevices<EffectProject> Effects(params EffectProject[] found)
    {
        var kept = new EffectProjects();

        kept.Keep(found);

        return kept;
    }

    /// <summary>A device with a face on it, for the drawing questions.</summary>
    private static Panel Faced()
    {
        var panel = new Panel();

        panel.Root.Children.Add(new PanelElement { Element = ElementKinds.Knob, Parameter = "cutoff" });

        return panel;
    }

    /// <summary>Both answer for an id they were given, and neither for one they were not.</summary>
    [Fact]
    public void Both_answer_for_what_they_were_given()
    {
        var machines = Machines(new SoundMachineProject { Id = "machine.oddskilla", Name = "OddSkilla" });
        var effects = Effects(new EffectProject { Id = "effect.echobox", Name = "EchoBox" });

        Assert.True(machines.Has("machine.oddskilla"));
        Assert.True(effects.Has("effect.echobox"));

        Assert.Equal("OddSkilla", machines.For("machine.oddskilla")!.Name);
        Assert.Equal("EchoBox", effects.For("effect.echobox")!.Name);

        Assert.False(machines.Has("effect.echobox"));
        Assert.False(effects.Has("machine.oddskilla"));
    }

    /// <summary>An id is an id whatever case it is typed in, in both worlds.</summary>
    [Fact]
    public void An_id_is_matched_without_case()
    {
        Assert.True(Machines(new SoundMachineProject { Id = "machine.zampler" }).Has("MACHINE.ZAMPLER"));
        Assert.True(Effects(new EffectProject { Id = "effect.echobox" }).Has("Effect.EchoBox"));
    }

    /// <summary>Nothing at all is nothing rather than a fault, on either.</summary>
    [Fact]
    public void Nothing_asked_for_is_nothing_found()
    {
        var machines = Machines();
        var effects = Effects();

        Assert.Null(machines.For(null));
        Assert.Null(effects.For(""));
        Assert.False(machines.Has(null));
        Assert.False(effects.Has(""));
        Assert.Empty(machines.All);
        Assert.Empty(effects.All);
        Assert.Null(machines.PanelFor("machine.oddskilla"));
        Assert.Null(effects.PanelFor("effect.echobox"));
    }

    /// <summary>A device with no id at all is not kept, since an id is what it is asked for by.</summary>
    [Fact]
    public void A_device_with_no_id_is_not_kept()
    {
        Assert.Empty(Machines(new SoundMachineProject { Id = "", Name = "Nameless" }).All);
        Assert.Empty(Effects(new EffectProject { Id = "", Name = "Nameless" }).All);
    }

    /// <summary>Keeping a new list forgets the one before it, which is what a reread is.</summary>
    /// <remarks>
    /// A device thrown out in SETTINGS has to be gone the moment the list is rebuilt rather than
    /// at the next start.
    /// </remarks>
    [Fact]
    public void Keeping_a_new_list_forgets_the_one_before()
    {
        var effects = new EffectProjects();

        effects.Keep(new[] { new EffectProject { Id = "effect.echobox" } });
        effects.Keep(new[] { new EffectProject { Id = "effect.other" } });

        Assert.False(effects.Has("effect.echobox"));
        Assert.True(effects.Has("effect.other"));
        Assert.Single(effects.All);
    }

    /// <summary>The order they were read in is the order they are listed in.</summary>
    [Fact]
    public void The_order_read_is_the_order_kept()
    {
        var effects = Effects(
            new EffectProject { Id = "effect.one" },
            new EffectProject { Id = "effect.two" },
            new EffectProject { Id = "effect.three" });

        Assert.Equal(new[] { "effect.one", "effect.two", "effect.three" },
            new List<string>(Array.ConvertAll(new List<EffectProject>(effects.All).ToArray(), one => one.Id)));
    }

    /// <summary>
    /// A face with something on it is a face; an empty one is nothing to draw.
    /// </summary>
    /// <remarks>
    /// The caller decides what to draw instead, which for a machine is the host's own plain panel.
    /// A panel with nothing on it drawn as a face is an empty frame nobody can use.
    /// </remarks>
    [Fact]
    public void A_face_with_nothing_on_it_is_nothing_to_draw()
    {
        var machines = Machines(
            new SoundMachineProject { Id = "machine.faced", Panel = Faced() },
            new SoundMachineProject { Id = "machine.bare" });

        var effects = Effects(
            new EffectProject { Id = "effect.faced", Panel = Faced() },
            new EffectProject { Id = "effect.bare" });

        Assert.NotNull(machines.PanelFor("machine.faced"));
        Assert.NotNull(effects.PanelFor("effect.faced"));

        Assert.Null(machines.PanelFor("machine.bare"));
        Assert.Null(effects.PanelFor("effect.bare"));
    }
}
