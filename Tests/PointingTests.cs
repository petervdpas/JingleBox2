using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Midi;
using JingleBox2.Views;
using System;
using Xunit;
using JingleBox2.Midi.Enums;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

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
    /// <summary>Walking a shelf of presets: which way, and where it stops.</summary>
    private readonly IPresetStep _step = new PresetStep();

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

    /// <summary>A strip's level names that strip, so eight faders are eight tracks.</summary>
    /// <remarks>
    /// It followed the cursor first, one shared template for every strip, and that could not be
    /// used on a desk with more than one fader: two links following the cursor have the same
    /// target, so the second replaced the first and pointing fader two at TR-02 unlinked fader
    /// one. Fixed, and named for its own track, which is also what the default layout does.
    /// </remarks>
    [Fact]
    public void A_strip_offers_its_own_track_rather_than_the_one_in_hand()
    {
        var second = MixLinks.On(MixControl.Volume, 1);

        Assert.Equal(ControlScope.Fixed, second.Scope);
        Assert.Equal(ControlKind.Mix, second.Kind);
        Assert.Equal(MixControl.Volume, second.Mix);
        Assert.Equal(1, second.Track);
    }

    /// <summary>
    /// And two strips' levels are two different targets, which is what makes eight links possible.
    /// </summary>
    /// <remarks>
    /// The fault itself, stated. A link displaces one that names the same target, which is right
    /// and is what stops a pile of links growing on one control. When every strip said "the track
    /// I am on", every level on the mixer was one target.
    /// </remarks>
    [Fact]
    public void Two_strips_levels_are_not_the_same_target()
    {
        Assert.False(MixLinks.On(MixControl.Volume, 0).SameTarget(MixLinks.On(MixControl.Volume, 1)));
        Assert.True(MixLinks.On(MixControl.Volume, 0).SameTarget(MixLinks.On(MixControl.Volume, 0)));
    }

    /// <summary>The master is strip -1 here as it is everywhere else.</summary>
    [Fact]
    public void The_master_is_the_strip_that_is_not_a_track()
    {
        var master = MixLinks.On(MixControl.Volume, JingleBox2.Tracker.TrackerPlayer.MasterStrip);

        Assert.Equal(ControlScope.Fixed, master.Scope);
        Assert.Equal(JingleBox2.Tracker.TrackerPlayer.MasterStrip, master.Track);
    }

    /// <summary>Pan, mute, solo and duck each name their own value rather than sharing one.</summary>
    [Fact]
    public void Every_control_on_a_strip_names_its_own_thing()
    {
        Assert.Equal(MixControl.Pan, MixLinks.On(MixControl.Pan, 0).Mix);
        Assert.Equal(MixControl.Mute, MixLinks.On(MixControl.Mute, 0).Mix);
        Assert.Equal(MixControl.Solo, MixLinks.On(MixControl.Solo, 0).Mix);
        Assert.Equal(MixControl.Duck, MixLinks.On(MixControl.Duck, 0).Mix);
    }

    /// <summary>The ducking release has a name for a link to use.</summary>
    /// <remarks>
    /// It was the one value on a strip a controller could not reach: every other control had a
    /// name for a link to use and the release time had none.
    /// </remarks>
    [Fact]
    public void The_release_time_can_be_pointed_at_too()
    {
        Assert.Equal(MixControl.Release, MixLinks.On(MixControl.Release, 0).Mix);
        Assert.Equal(ControlScope.Fixed, MixLinks.On(MixControl.Release, 0).Scope);
    }

    /// <summary>A shelf of presets is a list, so the picker offers two actions and no value.</summary>
    /// <remarks>
    /// The left half offers the one before and the right half the one after, because that is
    /// where the picker's own two arrows are.
    /// </remarks>
    [Fact]
    public void The_preset_picker_offers_the_step_the_hand_is_reaching_for()
    {
        Assert.Equal(PanelActions.PresetPrevious, _step.Side(10, 50));
        Assert.Equal(PanelActions.PresetNext, _step.Side(90, 50));
    }

    /// <summary>A step moves one place along the shelf, either way.</summary>
    [Fact]
    public void A_step_walks_the_shelf()
    {
        Assert.Equal(3, _step.Moved(2, 20, 1));
        Assert.Equal(1, _step.Moved(2, 20, -1));
    }

    /// <summary>The first and the last preset are where the walking stops.</summary>
    /// <remarks>
    /// Stopping rather than coming round. A button held down that wrapped would carry you past
    /// the one you were looking for without a pause to notice it.
    /// </remarks>
    [Fact]
    public void And_stops_at_either_end_rather_than_coming_round()
    {
        Assert.Equal(0, _step.Moved(0, 20, -1));
        Assert.Equal(19, _step.Moved(19, 20, 1));
    }

    /// <summary>With no preset picked, either direction lands on the first one.</summary>
    [Fact]
    public void A_step_with_nothing_picked_takes_the_first()
    {
        Assert.Equal(0, _step.Moved(-1, 20, 1));
        Assert.Equal(0, _step.Moved(-1, 20, -1));
    }

    /// <summary>A shelf with nothing on it keeps whatever index it was holding.</summary>
    [Fact]
    public void And_an_empty_shelf_is_left_alone()
    {
        Assert.Equal(4, _step.Moved(4, 0, 1));
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
        var template = MixLinks.On(MixControl.Volume, 0);

        var offered = ControlMapping.Copy(template);

        offered.Device = "Some Controller";
        offered.Cc = 74;

        Assert.Equal("", template.Device);
        Assert.Equal(0, template.Cc);
        Assert.Equal(MixControl.Volume, offered.Mix);
    }
}
