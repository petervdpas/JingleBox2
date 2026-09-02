using System;
using System.Threading;
using JingleBox2.Config;
using JingleBox2.Rack.Faces;
using JingleBox2.Tracker;
using JingleBox2.Devices.SoundMachines;
using JingleBox2.ViewModels;
using Xunit;
using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tests;

/// <summary>
/// Undo for the machine designer: a step is the machine as its own file would hold it.
/// </summary>
public class DesignHistoryTests
{
    /// <summary>
    /// A machine with one parameter and a panel with a root in it: enough for a step to have
    /// something in it that a test can move, and a folder so it can be shown not to travel.
    /// </summary>
    private static SoundMachineProject Machine()
    {
        var project = new SoundMachineProject { Id = "machine.test", Name = "Test", Folder = "/somewhere/real" };

        project.Parameters.Add(new Parameter { Key = "cutoff", Name = "Cutoff", Min = 0, Max = 1 });
        project.Panel = new Panel { Root = new PanelElement { Element = ElementKinds.Grid } };

        return project;
    }

    /// <summary>
    /// Opening is not an edit: there is nothing behind you and nothing owing to the folder.
    /// </summary>
    [Fact]
    public void A_machine_just_opened_has_nothing_to_undo_and_nothing_to_save()
    {
        var history = new DesignHistory();
        history.Opened(Machine());

        Assert.False(history.CanUndo);
        Assert.False(history.NeedsSaving);
    }

    /// <summary>
    /// Saving leaves nothing owing, even though saving itself moves the machine.
    /// </summary>
    /// <remarks>
    /// This is the fault it was written for. A save bumps the version on its way out, so the
    /// file carries 1.12 while the history went on believing the screen said 1.11, and the two
    /// then differ for ever: the Save button goes green and never goes back, and Cancel changes
    /// offers to throw away a change nobody made. What is on disc is what is on screen at the
    /// moment it is written, by definition, so saying so is the whole of the fix and it covers
    /// anything else a save does on its way past rather than only the version.
    /// </remarks>
    [Fact]
    public void Saving_leaves_nothing_owing_although_saving_moves_the_machine()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        project.Panel.Root.Children.Add(new PanelElement { Element = ElementKinds.Knob });
        history.Did(project);

        Assert.True(history.NeedsSaving);

        project.Version = "1.12";
        history.Saved(project);

        Assert.False(history.NeedsSaving);
    }

    /// <summary>And saving again, which is where the version really moves.</summary>
    /// <remarks>
    /// The first save of a session may not bump at all, so a fault here hides until the second
    /// press. Somebody who saves twice is somebody who has been working, which is exactly who
    /// notices a Save button that will not go cold.
    /// </remarks>
    [Fact]
    public void Saving_twice_leaves_nothing_owing_either()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        for (int at = 1; at <= 3; at++)
        {
            project.Panel.Root.Children.Add(new PanelElement { Element = ElementKinds.Knob });
            history.Did(project);

            project.Version = "1." + at;
            history.Saved(project);

            Assert.False(history.NeedsSaving);
        }
    }

    /// <summary>And an edit after a save still counts as one.</summary>
    /// <remarks>
    /// The other half of the same fix: saying the two are equal must not leave the history deaf
    /// to what happens next, or the button would go cold and stay cold.
    /// </remarks>
    [Fact]
    public void An_edit_after_a_save_still_needs_saving()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        project.Version = "1.12";
        history.Saved(project);

        Assert.False(history.NeedsSaving);

        project.Name = "Something else";
        history.Did(project);

        Assert.True(history.NeedsSaving);
    }

    /// <summary>
    /// The face, the parameters and the name are one document, so undo walks back through all
    /// three in the order they were done.
    /// </summary>
    [Fact]
    public void The_panel_the_parameters_and_the_name_are_all_one_document()
    {
        var project = Machine();
        var history = new DesignHistory();
        history.Opened(project);

        project.Panel.Root.Children.Add(new PanelElement { Element = ElementKinds.Knob });
        history.Did(project);

        project.Name = "Test Two";
        history.Did(project);

        project.Parameters.Add(new Parameter { Key = "resonance" });
        history.Did(project);

        history.Undo(project);
        Assert.Single(project.Parameters);

        history.Undo(project);
        Assert.Equal("Test", project.Name);

        history.Undo(project);
        Assert.Empty(project.Panel.Root.Children);

        Assert.False(history.CanUndo);
    }

    /// <summary>
    /// A field nobody named here comes back too, because a step is the machine as its own file
    /// would hold it.
    /// </summary>
    /// <remarks>
    /// Found rather than listed, because a list written out here would be right the day it
    /// was written and wrong the first time a field is added to a machine.
    /// </remarks>
    [Fact]
    public void A_field_nobody_listed_comes_back_too()
    {
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

    /// <summary>
    /// Where the machine lives is not part of what is being edited, and no step may put it back.
    /// </summary>
    /// <remarks>
    /// A step is the machine's own JSON, and a file does not carry its own path. Losing it
    /// would leave the editor with nowhere to save to.
    /// </remarks>
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

    /// <summary>Being told about a redraw is not being told about an edit.</summary>
    /// <remarks>
    /// The door is the editor's own redraw, which every edit ends at, so it is told more often
    /// than there are edits. A step per telling would fill the history with nothing to see.
    /// </remarks>
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

    /// <summary>Saving is what settles what is owing, and it settles nothing else.</summary>
    /// <remarks>
    /// And the history is still walkable: saving is not the same as forgetting.
    /// </remarks>
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

        Assert.True(history.CanUndo);
    }

    /// <summary>Cancel goes to what the folder holds, however many steps away that is.</summary>
    /// <remarks>
    /// Everything in the history was about a machine that no longer exists.
    /// </remarks>
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

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.False(history.NeedsSaving);
    }

    /// <summary>
    /// And it says so rather than doing nothing quietly, so the button can be dead when there
    /// is nothing to lose.
    /// </summary>
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
    /// <summary>
    /// An instrument, the values adapter a panel writes through, and a history wired to it the
    /// way the editor wires them, so a test can turn a knob the way a hand does rather than by
    /// telling the history what happened.
    /// </summary>
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

    /// <summary>
    /// Forty writes to one control inside the gathering window are one thing a person did.
    /// </summary>
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

    /// <summary>And coming back to the same control after a pause is a second gesture.</summary>
    /// <remarks>
    /// Gathered by the clock and by which control, deliberately not by a mouse button being
    /// held down: that is true of a mouse and false of a controller and of automation.
    /// </remarks>
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

    /// <summary>Moving to another control ends the gesture whatever the clock says.</summary>
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

    /// <summary>
    /// An undo pours into the instrument rather than replacing it, so the panel goes on writing
    /// to the thing the instrument owns.
    /// </summary>
    /// <remarks>
    /// The one that used to fail: putting a step back replaced the patch, and the panel's
    /// view model was left writing to an object the instrument no longer owned. After the
    /// undo here it is still the same values object, still reaching the instrument.
    /// </remarks>
    [Fact]
    public void What_the_panel_writes_and_what_the_voice_plays_stay_the_same_object()
    {
        var (instrument, values, history) = Synth();

        for (int at = 1; at <= 5; at++) values.Set("duty", 0.5 + at / 100.0);

        history.Undo(instrument);

        values.Set("duty", 0.9);

        Assert.Equal(0.9, values.Get("duty"), 4);
        Assert.True(history.CanUndo);
    }

    /// <summary>
    /// A plugin's own patch is left exactly where it was by an undo of the settings around it.
    /// </summary>
    /// <remarks>
    /// A step is the instrument as its file holds it minus the plugin state, because a
    /// described panel cannot move that anyway and carrying a third of a megabyte per step is
    /// paying for something nobody asked for. Left out of the step, and so left alone when one
    /// is put back.
    /// </remarks>
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
    /// <summary>
    /// A matrix of plain pads, named and at full level, with nothing loaded on them.
    /// </summary>
    private static System.Collections.Generic.List<PadConfig> Pads(int many)
    {
        var made = new System.Collections.Generic.List<PadConfig>();

        for (int at = 0; at < many; at++)
            made.Add(new PadConfig { Name = "Pad " + (at + 1), Volume = 1.0 });

        return made;
    }

    /// <summary>
    /// A step is every pad at once, so putting one back puts them all back as they were.
    /// </summary>
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

    /// <summary>
    /// Sixty writes to one pad's level are one step, because a level dragged is one gesture.
    /// </summary>
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

    /// <summary>
    /// Touching another pad ends the gathering, which is the rule the instrument knobs use.
    /// </summary>
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

    /// <summary>
    /// Changing how many pads there are is an edit, and it is about none of them.
    /// </summary>
    /// <remarks>
    /// This is what a history per pad could not have held, and the reason a step here is the
    /// whole matrix rather than one pad's settings.
    /// </remarks>
    [Fact]
    public void How_many_pads_there_are_is_an_edit_too()
    {
        var history = new PadHistory();
        history.Opened(Pads(8));

        history.Did(Pads(16), "");

        Assert.Equal(8, history.Undo()!.Count);
    }

    /// <summary>A pad that announces a change it did not make leaves nothing behind.</summary>
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
