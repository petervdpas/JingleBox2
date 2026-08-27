using System;
using System.Collections.ObjectModel;
using System.Threading;
using JingleBox2.Config;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Machines;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Undo for the machine designer: a step is the machine as its own file would hold it.
/// </summary>
public class DesignHistoryTests
{
    private static MachineProject Machine()
    {
        var project = new MachineProject { Id = "machine.test", Name = "Test", Folder = "/somewhere/real" };

        project.Parameters.Add(new MachineParameter { Key = "cutoff", Name = "Cutoff", Min = 0, Max = 1 });
        project.Panel = new MachinePanel { Root = new MachineElement { Element = MachineElementKinds.Grid } };

        return project;
    }

    [Fact]
    public void A_machine_just_opened_has_nothing_to_undo_and_nothing_to_save()
    {
        var history = new DesignHistory();
        history.Opened(Machine());

        Assert.False(history.CanUndo);
        Assert.False(history.NeedsSaving);
    }

    [Fact]
    public void The_panel_the_parameters_and_the_name_are_all_one_document()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        project.Panel.Root.Children.Add(new MachineElement { Element = MachineElementKinds.Knob });
        history.Did(project);

        project.Name = "Test Two";
        history.Did(project);

        project.Parameters.Add(new MachineParameter { Key = "resonance" });
        history.Did(project);

        history.Undo(project);
        Assert.Single(project.Parameters);

        history.Undo(project);
        Assert.Equal("Test", project.Name);

        history.Undo(project);
        Assert.Empty(project.Panel.Root.Children);

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void A_field_nobody_listed_comes_back_too()
    {
        // Found rather than listed, because a list written out here would be right the day it
        // was written and wrong the first time a field is added to a machine.
        var project = Machine();
        string bare = project.Theme.Accent;

        var history = new DesignHistory();
        history.Opened(project);

        project.Summary = "what it is for";
        project.Author = "somebody";
        project.Theme = project.Theme with { Accent = "#112233" };
        history.Did(project);

        history.Undo(project);

        Assert.Equal("", project.Summary);
        Assert.Equal("", project.Author);
        Assert.Equal(bare, project.Theme.Accent);
    }

    [Fact]
    public void The_folder_is_not_part_of_the_document_and_survives_every_step()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        project.Name = "Moved";
        history.Did(project);
        history.Undo(project);

        Assert.Equal("/somewhere/real", project.Folder);
    }

    [Fact]
    public void A_redraw_where_nothing_moved_leaves_no_step()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        history.Did(project);
        history.Did(project);
        history.Did(project);

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Saving_is_what_makes_the_folder_and_the_screen_agree()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        project.Name = "Changed";
        history.Did(project);

        Assert.True(history.NeedsSaving);

        history.Saved(project);

        Assert.False(history.NeedsSaving);

        // And the history is still walkable: saving is not the same as forgetting.
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void Cancelling_goes_to_the_folder_rather_than_back_one_step()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        history.Saved(project);

        project.Name = "One"; history.Did(project);
        project.Name = "Two"; history.Did(project);
        project.Name = "Three"; history.Did(project);

        Assert.True(history.Cancel(project));

        Assert.Equal("Test", project.Name);

        // Everything in the history was about a machine that no longer exists.
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.False(history.NeedsSaving);
    }

    [Fact]
    public void There_is_nothing_to_cancel_when_nothing_has_changed()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        Assert.False(history.Cancel(project));
    }
}

/// <summary>
/// Undo for an instrument's knobs, which is the one that has to turn a stream into a gesture.
/// </summary>
public class InstrumentHistoryTests
{
    private static (TrackerInstrument Instrument, SynthValues Values, InstrumentHistory History) Synth()
    {
        var instrument = new TrackerInstrument { Name = "OddSkilla", Kind = TrackerInstrumentKind.Synth };
        instrument.EnsureId();

        var voice = new SynthPatchViewModel(instrument.Patch, () => { });
        var values = new SynthValues(voice, instrument);
        var history = new InstrumentHistory();

        values.Said += key => history.Did(instrument, key);
        history.Opened(instrument);

        return (instrument, values, history);
    }

    [Fact]
    public void A_knob_dragged_across_its_range_is_one_step()
    {
        var (instrument, values, history) = Synth();

        double was = values.Get("duty");

        for (int at = 1; at <= 40; at++) values.Set("duty", 0.5 + at / 100.0);

        Assert.NotEqual(was, values.Get("duty"));

        history.Undo(instrument);

        Assert.Equal(was, values.Get("duty"), 4);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Letting_go_and_touching_it_again_is_two()
    {
        var (instrument, values, history) = Synth();

        for (int at = 1; at <= 10; at++) values.Set("duty", 0.1 + at / 200.0);

        Thread.Sleep(InstrumentHistory.SameGesture + TimeSpan.FromMilliseconds(200));

        for (int at = 1; at <= 10; at++) values.Set("duty", 0.5 + at / 200.0);

        int steps = 0;
        while (history.CanUndo) { history.Undo(instrument); steps++; }

        Assert.Equal(2, steps);
    }

    [Fact]
    public void A_different_control_is_always_a_new_gesture()
    {
        var (instrument, values, history) = Synth();

        values.Set("duty", 0.31);
        values.Set("tune", 5);
        values.Set("duty", 0.62);

        int steps = 0;
        while (history.CanUndo) { history.Undo(instrument); steps++; }

        Assert.Equal(3, steps);
    }

    [Fact]
    public void What_the_panel_writes_and_what_the_voice_plays_stay_the_same_object()
    {
        // The one that used to fail: putting a step back replaced the patch, and the panel's
        // view model was left writing to an object the instrument no longer owned.
        var (instrument, values, history) = Synth();

        for (int at = 1; at <= 5; at++) values.Set("duty", 0.5 + at / 100.0);

        history.Undo(instrument);

        // Still the same values object, still reaching the instrument.
        values.Set("duty", 0.9);

        Assert.Equal(0.9, values.Get("duty"), 4);
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void A_plugins_own_patch_is_neither_kept_nor_cleared()
    {
        var instrument = new TrackerInstrument { Name = "Serum", Kind = TrackerInstrumentKind.Plugin };
        instrument.EnsureId();
        instrument.PluginState = new byte[300_000];
        instrument.PluginState[7] = 42;

        var history = new InstrumentHistory();
        history.Opened(instrument);

        instrument.Volume = 0.5;
        history.Did(instrument, "volume");

        instrument.Volume = 0.9;
        history.Did(instrument, "name");

        history.Undo(instrument);

        Assert.Equal(0.5, instrument.Volume);
        Assert.Equal(300_000, instrument.PluginState.Length);
        Assert.Equal(42, instrument.PluginState[7]);
    }
}

/// <summary>Undo for the pads, where a step is every pad at once.</summary>
public class PadHistoryTests
{
    private static System.Collections.Generic.List<PadConfig> Pads(int many)
    {
        var made = new System.Collections.Generic.List<PadConfig>();

        for (int at = 0; at < many; at++)
            made.Add(new PadConfig { Name = "Pad " + (at + 1), Volume = 1.0 });

        return made;
    }

    [Fact]
    public void Naming_a_pad_is_a_step_and_leaves_the_others_alone()
    {
        var pads = Pads(8);
        var history = new PadHistory();
        history.Opened(pads);

        pads[2].Name = "Airhorn";
        history.Did(pads, "2.Name");

        var back = history.Undo();

        Assert.Equal("Pad 3", back![2].Name);
        Assert.Equal("Pad 1", back[0].Name);
    }

    [Fact]
    public void A_level_dragged_is_one_step()
    {
        var pads = Pads(8);
        var history = new PadHistory();
        history.Opened(pads);

        for (int at = 1; at <= 60; at++)
        {
            pads[0].Volume = at / 100.0;
            history.Did(pads, "0.Volume");
        }

        Assert.Equal(1.0, history.Undo()![0].Volume);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void A_different_pad_is_a_different_gesture()
    {
        var pads = Pads(8);
        var history = new PadHistory();
        history.Opened(pads);

        pads[0].Volume = 0.2; history.Did(pads, "0.Volume");
        pads[1].Volume = 0.3; history.Did(pads, "1.Volume");
        pads[0].Volume = 0.4; history.Did(pads, "0.Volume");

        int steps = 0;
        while (history.CanUndo) { history.Undo(); steps++; }

        Assert.Equal(3, steps);
    }

    [Fact]
    public void How_many_pads_there_are_is_an_edit_too()
    {
        var history = new PadHistory();
        history.Opened(Pads(8));

        history.Did(Pads(16), "");

        Assert.Equal(8, history.Undo()!.Count);
    }

    [Fact]
    public void A_say_where_nothing_moved_leaves_no_step()
    {
        var pads = Pads(8);
        var history = new PadHistory();
        history.Opened(pads);

        history.Did(pads, "0.Volume");
        history.Did(pads, "1.Name");

        Assert.False(history.CanUndo);
    }
}
