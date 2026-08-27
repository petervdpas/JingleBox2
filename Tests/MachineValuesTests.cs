using System.Collections.ObjectModel;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Machines;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A machine's settings, and the rule the base class exists to enforce.
/// </summary>
/// <remarks>
/// A machine cannot move a value without announcing it, because <c>Set</c> is sealed and does
/// the announcing. Left to each machine to remember, one of them would not, and the way that
/// fails is the worst kind: the sound is right and the picture is wrong.
/// </remarks>
public class MachineValuesTests
{
    private static (TrackerInstrument Instrument, SynthValues Values) Synth()
    {
        var instrument = new TrackerInstrument { Name = "OddSkilla", Kind = TrackerInstrumentKind.Synth };
        instrument.EnsureId();

        return (instrument, new SynthValues(new SynthPatchViewModel(instrument.Patch, () => { }), instrument));
    }

    [Fact]
    public void The_owner_and_anything_showing_them_are_both_told()
    {
        var (_, values) = Synth();

        int owner = 0, onlooker = 0;
        string? which = null;

        values.Changed = () => owner++;
        values.Said += key => { onlooker++; which = key; };

        values.Set("duty", 0.42);

        Assert.Equal(1, owner);
        Assert.Equal(1, onlooker);
        Assert.Equal("duty", which);
    }

    [Fact]
    public void Saying_the_same_thing_again_says_nothing()
    {
        var (_, values) = Synth();

        values.Set("duty", 0.42);

        int said = 0;
        values.Said += _ => said++;

        // A controller reporting the position it already holds must not mark a song unsaved.
        values.Set("duty", 0.42);

        Assert.Equal(0, said);
    }

    [Fact]
    public void There_can_be_any_number_of_onlookers()
    {
        var (_, values) = Synth();

        int first = 0, second = 0;

        values.Said += _ => first++;
        values.Said += _ => second++;

        values.Set("duty", 0.31);

        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void A_value_that_is_not_a_number_is_refused()
    {
        var (_, values) = Synth();

        double was = values.Get("duty");

        // A knob cannot produce one; a file can, and a NaN reaching a voice spreads through the
        // filter and silences the instrument for good.
        values.Set("duty", double.NaN);

        Assert.Equal(was, values.Get("duty"));
    }

    [Fact]
    public void A_setting_the_machine_has_never_heard_of_changes_nothing()
    {
        var (_, values) = Synth();

        int said = 0;
        values.Said += _ => said++;

        values.Set("nothing at all", 1);

        Assert.Equal(0, said);
    }

    [Fact]
    public void The_racks_preview_announces_itself_the_same_way()
    {
        var shown = new ObservableCollection<MachineParameterViewModel>
        {
            new(new MachineParameter { Key = "duty", Min = 0, Max = 1 })
        };

        var values = new MachinePreviewValues(shown);

        int said = 0;
        values.Said += _ => said++;

        values.Set("duty", 0.7);
        Assert.Equal(1, said);

        values.Set("duty", 0.7);
        Assert.Equal(1, said);
    }
}
