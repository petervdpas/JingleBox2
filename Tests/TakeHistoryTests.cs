using System.Linq;
using JingleBox2.Audio;
using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What has been done to a take and where in it you are standing, asked without a take.
/// </summary>
/// <remarks>
/// The awkward case is the one worth pinning: a hand goes back three steps and then does
/// something new, at which point the three it went back past are a take that no longer exists
/// and there is nothing honest to do with them. Every editor anywhere drops them, and the way
/// that goes wrong is a redo that puts back a step which was undone against different audio.
/// </remarks>
public sealed class TakeHistoryTests
{
    /// <summary>The history under test.</summary>
    private readonly ITakeHistory _history = new TakeHistory();

    /// <summary>A step of a kind, since which kind it is decides nothing here.</summary>
    /// <param name="kind">Which edit.</param>
    /// <returns>The step.</returns>
    private static TakeStep Step(TakeEditKind kind) => new(kind, 0, 100, -1);

    /// <summary>A fresh history has nothing to go back past.</summary>
    [Fact]
    public void Nothing_has_been_done_yet()
    {
        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
        Assert.Equal(0, _history.Done);
    }

    /// <summary>A step done is a step to go back past.</summary>
    [Fact]
    public void A_step_can_be_gone_back_past()
    {
        _history.Add(Step(TakeEditKind.Trim));

        Assert.True(_history.CanUndo);
        Assert.Equal(1, _history.Done);
    }

    /// <summary>Going back leaves the step where it is, so it can be done again.</summary>
    [Fact]
    public void Going_back_keeps_the_step()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Undo();

        Assert.Equal(0, _history.Done);
        Assert.Single(_history.Steps);
        Assert.True(_history.CanRedo);
    }

    /// <summary>And going forward stands on it again.</summary>
    [Fact]
    public void Going_forward_stands_on_it_again()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Undo();
        _history.Redo();

        Assert.Equal(1, _history.Done);
        Assert.False(_history.CanRedo);
    }

    /// <summary>
    /// **Something new done after going back throws away what was in front.**
    /// </summary>
    /// <remarks>
    /// The one that matters. What was in front was done to audio that no longer exists, so a
    /// redo of it would be putting a step back against a different take.
    /// </remarks>
    [Fact]
    public void Doing_something_new_drops_what_was_in_front()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));
        _history.Undo();

        _history.Add(Step(TakeEditKind.FadeIn));

        Assert.Equal(2, _history.Steps.Count);
        Assert.Equal(TakeEditKind.FadeIn, _history.Steps[1].Kind);
        Assert.False(_history.CanRedo);
    }

    /// <summary>Only the steps behind where you stand count as done.</summary>
    [Fact]
    public void What_has_been_applied_is_what_is_behind_you()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));
        _history.Add(Step(TakeEditKind.Normalize));
        _history.GoTo(2);

        Assert.Equal(
            new[] { TakeEditKind.Trim, TakeEditKind.Reverse },
            _history.Applied.Select(step => step.Kind));
    }

    /// <summary>Standing at the top is standing on the take as it was found.</summary>
    [Fact]
    public void The_top_of_the_list_is_the_take_itself()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.GoTo(0);

        Assert.Empty(_history.Applied);
        Assert.False(_history.CanUndo);
    }

    /// <summary>A place past either end of the list is held inside it.</summary>
    [Fact]
    public void A_place_outside_the_list_is_brought_inside_it()
    {
        _history.Add(Step(TakeEditKind.Trim));

        _history.GoTo(50);
        Assert.Equal(1, _history.Done);

        _history.GoTo(-9);
        Assert.Equal(0, _history.Done);
    }

    /// <summary>Going back at the top and forward at the end do nothing rather than throwing.</summary>
    [Fact]
    public void The_two_ends_hold()
    {
        _history.Undo();
        Assert.Equal(0, _history.Done);

        _history.Add(Step(TakeEditKind.Trim));
        _history.Redo();
        Assert.Equal(1, _history.Done);
    }

    /// <summary>
    /// **Walking back to the top is a fresh copy and nothing done to it.**
    /// </summary>
    /// <remarks>
    /// The one that crashed. Worked out in the page, the step to do again was read before
    /// anything had decided which way the walk went, so going to the top read the step before
    /// the first one: an index of minus one, from the Revert button, taking the application
    /// down. Nothing could reach it, because the arithmetic was in a view model that needs a
    /// window and a disc to build.
    /// </remarks>
    [Fact]
    public void Walking_to_the_top_is_a_fresh_copy_and_no_steps()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));
        _history.Add(Step(TakeEditKind.FadeIn));

        var walk = _history.Toward(0);

        Assert.True(walk.Fresh);
        Assert.Empty(walk.Steps);
    }

    /// <summary>And so is a walk to the top of an empty history, which asks for nothing at all.</summary>
    [Fact]
    public void Walking_to_the_top_of_nothing_asks_for_nothing()
    {
        var walk = _history.Toward(0);

        Assert.True(walk.Idle);
        Assert.Empty(walk.Steps);
    }

    /// <summary>Going forward by one is that step done to the take as it already is.</summary>
    [Fact]
    public void Going_forward_one_step_does_not_start_again()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));
        _history.GoTo(1);

        var walk = _history.Toward(2);

        Assert.False(walk.Fresh);
        Assert.Equal(TakeEditKind.Reverse, Assert.Single(walk.Steps).Kind);
    }

    /// <summary>Going back one step is the take again with everything before it done.</summary>
    [Fact]
    public void Going_back_starts_from_the_take_again()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));

        var walk = _history.Toward(1);

        Assert.True(walk.Fresh);
        Assert.Equal(TakeEditKind.Trim, Assert.Single(walk.Steps).Kind);
    }

    /// <summary>Going forward by more than one starts again too, since nothing else is in order.</summary>
    [Fact]
    public void Going_forward_two_steps_starts_from_the_take_again()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));
        _history.Add(Step(TakeEditKind.FadeIn));
        _history.GoTo(0);

        var walk = _history.Toward(3);

        Assert.True(walk.Fresh);
        Assert.Equal(3, walk.Steps.Count);
    }

    /// <summary>A walk to where you are standing is no work.</summary>
    [Fact]
    public void A_walk_to_here_is_no_work()
    {
        _history.Add(Step(TakeEditKind.Trim));

        Assert.True(_history.Toward(1).Idle);
    }

    /// <summary>A walk past either end is held inside the list rather than throwing.</summary>
    [Fact]
    public void A_walk_outside_the_list_is_brought_inside_it()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));
        _history.GoTo(0);

        Assert.Equal(2, _history.Toward(90).Steps.Count);

        _history.GoTo(2);

        var back = _history.Toward(-4);

        Assert.True(back.Fresh);
        Assert.Empty(back.Steps);
    }

    /// <summary>Asking what a walk would take moves nobody.</summary>
    [Fact]
    public void Asking_about_a_walk_moves_nothing()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));

        _history.Toward(0);

        Assert.Equal(2, _history.Done);
    }

    /// <summary>Clearing leaves nothing, which is what saving does.</summary>
    [Fact]
    public void Clearing_leaves_nothing()
    {
        _history.Add(Step(TakeEditKind.Trim));
        _history.Add(Step(TakeEditKind.Reverse));
        _history.Clear();

        Assert.Empty(_history.Steps);
        Assert.Equal(0, _history.Done);
        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }
}
