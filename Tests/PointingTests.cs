using JingleBox2.Machines;
using JingleBox2.Midi;
using JingleBox2.Views;
using System;
using Xunit;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Tests;

/// <summary>
/// The mouse mode: when the gesture is answered, and what a mixer strip offers.
/// </summary>
/// <remarks>
/// The keystroke itself needs a window and a keyboard. The rule inside it needs neither, which
/// is why it is a method of its own: everything above it is Avalonia and everything below it is
/// a decision.
///
/// Four groups, in this order: when Ctrl+Shift+M is answered, what a mixer strip offers a knob,
/// the two actions a preset picker offers instead of a value, and the rule that what is offered
/// is a copy.
/// </remarks>
public class PointingTests
{
    /// <summary>The mode is offered where there is at least one thing on screen to point at.</summary>
    /// <remarks>
    /// A pointable control joins the tally when it comes on screen and leaves it when it goes,
    /// so the mixer counts and so does a machine's panel.
    /// </remarks>
    [Fact]
    public void The_gesture_is_answered_where_there_is_something_to_point_at()
    {
        Assert.True(LinkKey.Answers(true, TimeSpan.FromSeconds(1)));
    }

    /// <summary>And refused on a page with nothing pointable on it.</summary>
    /// <remarks>
    /// A keystroke that does nothing here may mean something to whatever is in front of you, so
    /// it is refused rather than swallowed.
    /// </remarks>
    [Fact]
    public void And_not_where_there_is_nothing()
    {
        Assert.False(LinkKey.Answers(false, TimeSpan.FromSeconds(1)));
    }

    /// <summary>A repeat arriving too soon after the last one is not a second gesture.</summary>
    /// <remarks>
    /// A key leant on repeats at about thirty a second, and a mode flapping thirty times a
    /// second is a mode nobody can put where they want it.
    /// </remarks>
    [Fact]
    public void A_key_leant_on_is_one_gesture()
    {
        Assert.False(LinkKey.Answers(true, TimeSpan.FromMilliseconds(30)));
    }

    /// <summary>And a press after the gap has gone by turns the mode over again.</summary>
    /// <remarks>
    /// This is the half that was wrong. It used to be a flag saying the key was down, cleared
    /// by the key coming up, and the key can come up somewhere else: focus moves while it is
    /// held, the release goes to whatever took the focus, and the flag stays set for ever. From
    /// then on every press was swallowed and the mode was stuck in whatever state it was left.
    /// A clock cannot be stranded.
    /// </remarks>
    [Fact]
    public void And_pressing_again_afterwards_is_another()
    {
        Assert.True(LinkKey.Answers(true, TimeSpan.FromMilliseconds(LinkKey.AgainMs)));
    }

    /// <summary>A strip's level is offered for whichever track you last touched.</summary>
    /// <remarks>
    /// Focused rather than a track number, so one knob pointed at Level is the level of the
    /// strip in hand rather than one link per track.
    /// </remarks>
    [Fact]
    public void A_strip_offers_the_track_you_are_on_rather_than_a_numbered_one()
    {
        Assert.Equal(ControlScope.Focused, MixLinks.Level.Scope);
        Assert.Equal(ControlKind.Mix, MixLinks.Level.Kind);
        Assert.Equal(MixControl.Volume, MixLinks.Level.Mix);
    }

    /// <summary>Pan, mute, solo and duck each name their own value rather than sharing one.</summary>
    [Fact]
    public void Every_control_on_a_strip_names_its_own_thing()
    {
        Assert.Equal(MixControl.Pan, MixLinks.Pan.Mix);
        Assert.Equal(MixControl.Mute, MixLinks.Mute.Mix);
        Assert.Equal(MixControl.Solo, MixLinks.Solo.Mix);
        Assert.Equal(MixControl.Duck, MixLinks.Duck.Mix);
    }

    /// <summary>The ducking release has a name for a link to use.</summary>
    /// <remarks>
    /// It was the one value on a strip a controller could not reach: every other control had a
    /// name for a link to use and the release time had none.
    /// </remarks>
    [Fact]
    public void The_release_time_can_be_pointed_at_too()
    {
        Assert.Equal(MixControl.Release, MixLinks.Release.Mix);
        Assert.Equal(ControlScope.Focused, MixLinks.Release.Scope);
    }

    /// <summary>A shelf of presets is a list, so the picker offers two actions and no value.</summary>
    /// <remarks>
    /// The left half offers the one before and the right half the one after, because that is
    /// where the picker's own two arrows are.
    /// </remarks>
    [Fact]
    public void The_preset_picker_offers_the_step_the_hand_is_reaching_for()
    {
        Assert.Equal(MachineActions.PresetPrevious, PresetStep.Side(10, 50));
        Assert.Equal(MachineActions.PresetNext, PresetStep.Side(90, 50));
    }

    /// <summary>A step moves one place along the shelf, either way.</summary>
    [Fact]
    public void A_step_walks_the_shelf()
    {
        Assert.Equal(3, PresetStep.Moved(2, 20, 1));
        Assert.Equal(1, PresetStep.Moved(2, 20, -1));
    }

    /// <summary>The first and the last preset are where the walking stops.</summary>
    /// <remarks>
    /// Stopping rather than coming round. A button held down that wrapped would carry you past
    /// the one you were looking for without a pause to notice it.
    /// </remarks>
    [Fact]
    public void And_stops_at_either_end_rather_than_coming_round()
    {
        Assert.Equal(0, PresetStep.Moved(0, 20, -1));
        Assert.Equal(19, PresetStep.Moved(19, 20, 1));
    }

    /// <summary>With no preset picked, either direction lands on the first one.</summary>
    [Fact]
    public void A_step_with_nothing_picked_takes_the_first()
    {
        Assert.Equal(0, PresetStep.Moved(-1, 20, 1));
        Assert.Equal(0, PresetStep.Moved(-1, 20, -1));
    }

    /// <summary>A shelf with nothing on it keeps whatever index it was holding.</summary>
    [Fact]
    public void And_an_empty_shelf_is_left_alone()
    {
        Assert.Equal(4, PresetStep.Moved(4, 0, 1));
    }

    /// <summary>Offering a mapping hands out a copy and leaves the template as it shipped.</summary>
    /// <remarks>
    /// What is offered has to be a copy, because a link fills the controller's half into the
    /// object it was given and then keeps it. Handing out the template would have every link
    /// after the first overwriting the one before.
    /// </remarks>
    [Fact]
    public void What_is_offered_is_a_copy_and_the_template_is_left_alone()
    {
        var offered = ControlMapping.Copy(MixLinks.Level);

        offered.Device = "Some Controller";
        offered.Cc = 74;

        Assert.Equal("", MixLinks.Level.Device);
        Assert.Equal(0, MixLinks.Level.Cc);
        Assert.Equal(MixControl.Volume, offered.Mix);
    }
}
