using System;
using System.Linq;
using System.Reflection;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.SoundDevices;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundMachines;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which parts a face may carry, and the rule that decides it.
/// </summary>
/// <remarks>
/// A part is a control plus whatever the host has to supply behind it, so a part whose service a
/// box cannot answer is a control nothing will ever fill. The designer offered every part to both
/// worlds, which meant a keyboard could be dropped on a delay, drawn, saved into the manifest,
/// and silent for ever with nothing anywhere saying why.
///
/// What is checked here is the rule rather than the list: that the effect's parts are the whole
/// list minus the played ones and nothing else, that the order does not move, and that every word
/// in either list is a kind the panel really knows. The last of those is the one that catches a
/// typo, which would otherwise take a part out of the designer silently.
/// </remarks>
public class PanelPartsTests
{
    /// <summary>The rules under test. They hold nothing, so one apiece is enough.</summary>
    private readonly PanelParts _parts = new();

    /// <summary>Every word in either list is a kind the panel knows.</summary>
    /// <remarks>
    /// Read off <see cref="ElementKinds"/> rather than written out again, since a list written
    /// twice is a list that will disagree.
    /// </remarks>
    [Fact]
    public void Every_part_named_is_a_kind_the_panel_knows()
    {
        var known = typeof(ElementKinds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(one => one.IsLiteral && one.FieldType == typeof(string))
            .Select(one => (string)one.GetValue(null)!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(_parts.All, one => Assert.Contains(one, known));
        Assert.All(_parts.NeedNotes, one => Assert.Contains(one, known));
    }

    /// <summary>Nothing is in the list twice, which would draw the same part twice.</summary>
    [Fact]
    public void No_part_is_listed_twice()
    {
        Assert.Equal(_parts.All.Count, _parts.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(_parts.NeedNotes.Count, _parts.NeedNotes.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The played ones are all really in the list they are taken out of.</summary>
    [Fact]
    public void Every_played_part_is_one_of_the_parts()
    {
        Assert.All(_parts.NeedNotes, one => Assert.Contains(one, _parts.All));
    }

    /// <summary>What is left is the whole list minus the played ones, in the same order.</summary>
    [Fact]
    public void What_is_left_keeps_the_order_it_had()
    {
        Assert.Equal(_parts.All.Where(one => !_parts.NeedNotes.Contains(one)), _parts.For(played: false));
        Assert.Equal(_parts.All.Count - _parts.NeedNotes.Count, _parts.For(played: false).Count);
        Assert.Equal(_parts.All, _parts.For(played: true));
    }

    /// <summary>A soundmachine is played, so it may carry everything.</summary>
    [Fact]
    public void A_soundmachine_may_carry_every_part()
    {
        Assert.Equal(_parts.All, new SoundMachineWorld().Parts);
    }

    /// <summary>An effect may carry none of the parts that need notes or a kit.</summary>
    [Fact]
    public void An_effect_carries_nothing_that_needs_notes_or_a_kit()
    {
        var offered = new SoundEffectWorld().Parts;

        Assert.DoesNotContain(ElementKinds.Keys, offered);
        Assert.DoesNotContain(ElementKinds.Pads, offered);
        Assert.DoesNotContain(ElementKinds.Pad, offered);
        Assert.DoesNotContain(ElementKinds.PadPicker, offered);
        Assert.DoesNotContain(ElementKinds.Zones, offered);
        Assert.DoesNotContain(ElementKinds.ZonePicker, offered);
        Assert.DoesNotContain(ElementKinds.Slices, offered);
        Assert.DoesNotContain(ElementKinds.InstrumentName, offered);
    }

    /// <summary>
    /// And it still carries everything an effect really can have.
    /// </summary>
    /// <remarks>
    /// The half of the rule that is easy to get wrong in the other direction. A scope, a preset
    /// picker, a take picker and a waveform are all ordinary on an effect and are only unwired
    /// here, so leaving them out would write a gap in this application into what an effect is.
    /// </remarks>
    [Fact]
    public void An_effect_still_carries_everything_it_can_have()
    {
        var offered = new SoundEffectWorld().Parts;

        foreach (string one in new[]
                 {
                     ElementKinds.Knob, ElementKinds.Fader, ElementKinds.Switch, ElementKinds.Number,
                     ElementKinds.Button, ElementKinds.Choice, ElementKinds.Led, ElementKinds.Meter,
                     ElementKinds.Grid, ElementKinds.Group, ElementKinds.Row, ElementKinds.Column,
                     ElementKinds.Strip, ElementKinds.Menu, ElementKinds.Label, ElementKinds.Text,
                     ElementKinds.Spacer, ElementKinds.Image, ElementKinds.Scope, ElementKinds.Preset,
                     ElementKinds.Take, ElementKinds.Wave, ElementKinds.Envelope, ElementKinds.Location
                 })
            Assert.Contains(one, offered);
    }

    /// <summary>The two worlds differ by exactly the played parts and by nothing else.</summary>
    [Fact]
    public void The_two_worlds_differ_by_the_played_parts_alone()
    {
        var machine = new SoundMachineWorld().Parts;
        var effect = new SoundEffectWorld().Parts;

        Assert.Equal(_parts.NeedNotes.OrderBy(one => one, StringComparer.Ordinal),
                     machine.Except(effect).OrderBy(one => one, StringComparer.Ordinal));
        Assert.Empty(effect.Except(machine));
    }
}
