using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The rules a machine's panel is built and read by, which are the contract an outside machine
/// is drawn against.
/// </summary>
/// <remarks>
/// These decide what a controller nobody has written a file for does the moment it is plugged
/// in, what a pad says it answers to, and what a panel prints under a switch. All three are
/// read by eye and none of them says anything when it is wrong.
/// </remarks>
public class PanelRulesTests
{
    private readonly IPanelOrder _order = new PanelOrder();
    private readonly IPanelNotes _notes = new PanelNotes();
    private readonly IPresetStep _step = new PresetStep();
    private readonly INaming _naming = new Naming();

    private static PanelElement Knob(string parameter) =>
        new() { Element = ElementKinds.Knob, Parameter = parameter };

    private static PanelElement Holding(params PanelElement[] children)
    {
        var group = new PanelElement { Element = ElementKinds.Group };

        foreach (var child in children) group.Children.Add(child);

        return group;
    }

    /// <summary>No panel at all is no order, rather than a throw.</summary>
    [Fact]
    public void Nothing_has_no_order()
    {
        Assert.Empty(_order.Of(null));
        Assert.Empty(_order.Of(new Panel()));
        Assert.Equal("", _order.At(null, 0));
    }

    /// <summary>The order is depth first, which is the order an eye goes over a face.</summary>
    [Fact]
    public void The_order_is_depth_first()
    {
        var panel = new Panel
        {
            Root = Holding(
                Holding(Knob("cutoff"), Knob("resonance")),
                Knob("drive"))
        };

        Assert.Equal(new[] { "cutoff", "resonance", "drive" }, _order.Of(panel));
    }

    /// <summary>
    /// A parameter named twice counts once and keeps the place of the first.
    /// </summary>
    /// <remarks>
    /// A value shown beside the knob that turns it is two elements naming one parameter, and a
    /// controller pointed at "the third control" must not find the same one twice.
    /// </remarks>
    [Fact]
    public void A_parameter_named_twice_counts_once()
    {
        var panel = new Panel
        {
            Root = Holding(Knob("cutoff"), Knob("drive"), Knob("cutoff"), Knob("mix"))
        };

        Assert.Equal(new[] { "cutoff", "drive", "mix" }, _order.Of(panel));
        Assert.Equal("mix", _order.At(panel, 2));
    }

    /// <summary>Elements naming no parameter are not in the order at all.</summary>
    [Fact]
    public void A_label_is_not_in_the_order()
    {
        var panel = new Panel
        {
            Root = Holding(
                new PanelElement { Element = ElementKinds.Label },
                Knob("cutoff"))
        };

        Assert.Equal(new[] { "cutoff" }, _order.Of(panel));
    }

    /// <summary>Asking past the end, or before the start, is nothing rather than a throw.</summary>
    [Fact]
    public void Asking_outside_the_order_is_nothing()
    {
        var panel = new Panel { Root = Holding(Knob("cutoff")) };

        Assert.Equal("cutoff", _order.At(panel, 0));
        Assert.Equal("", _order.At(panel, 1));
        Assert.Equal("", _order.At(panel, 40));
        Assert.Equal("", _order.At(panel, -1));
    }

    /// <summary>Every note this app can play survives being written down and read back.</summary>
    /// <remarks>
    /// The two halves are a pair, and a pair that disagrees is a pad that sounds a different
    /// note from the one printed on it. All 120 rather than a handful, since the failure would
    /// be one octave or one accidental rather than the whole scale.
    /// </remarks>
    [Fact]
    public void Every_note_survives_the_round_trip()
    {
        for (int semitone = 0; semitone <= 119; semitone++)
        {
            string said = _notes.Name(semitone);

            Assert.Equal(3, said.Length);
            Assert.Equal(semitone, _notes.Semitone(said));
        }
    }

    /// <summary>Naturals carry a hyphen, so every note is the same width in a column.</summary>
    [Fact]
    public void Every_note_is_the_same_width()
    {
        Assert.Equal("C-0", _notes.Name(0));
        Assert.Equal("C#0", _notes.Name(1));
        Assert.Equal("B-9", _notes.Name(119));
    }

    /// <summary>A semitone off either end is nothing rather than a wrong note.</summary>
    [Fact]
    public void A_semitone_off_the_end_is_nothing()
    {
        Assert.Equal("", _notes.Name(-1));
        Assert.Equal("", _notes.Name(120));
        Assert.Equal("", _notes.Name(int.MaxValue));
        Assert.Equal("", _notes.Name(int.MinValue));
    }

    /// <summary>A plain number is read too, for a machine written before notes were spelled out.</summary>
    [Fact]
    public void A_plain_number_is_still_read()
    {
        Assert.Equal(60, _notes.Semitone("60"));
        Assert.Equal(0, _notes.Semitone("0"));
        Assert.Equal(119, _notes.Semitone("119"));
        Assert.Equal(-1, _notes.Semitone("120"));
        Assert.Equal(-1, _notes.Semitone("-1"));
    }

    /// <summary>Anything that is not a note is refused rather than guessed at.</summary>
    [Fact]
    public void Something_that_is_not_a_note_is_refused()
    {
        foreach (string said in new[] { "", "  ", "H-4", "C", "C-", "C-99", "C$4", "Cb4", null! })
            Assert.Equal(-1, _notes.Semitone(said));
    }

    /// <summary>A note is read whatever case it is written in.</summary>
    [Fact]
    public void A_note_reads_in_either_case()
    {
        Assert.Equal(_notes.Semitone("C#4"), _notes.Semitone("c#4"));
        Assert.Equal(_notes.Semitone("A-3"), _notes.Semitone("a-3"));
    }

    /// <summary>A step lands where it should, and stops at both ends rather than coming round.</summary>
    /// <remarks>
    /// A button held down that wrapped would carry you past the one you were looking for
    /// without a pause to notice it.
    /// </remarks>
    [Fact]
    public void A_step_stops_at_the_ends()
    {
        Assert.Equal(3, _step.Moved(2, 8, 1));
        Assert.Equal(1, _step.Moved(2, 8, -1));

        Assert.Equal(7, _step.Moved(7, 8, 1));
        Assert.Equal(0, _step.Moved(0, 8, -1));

        Assert.Equal(7, _step.Moved(2, 8, 40));
        Assert.Equal(0, _step.Moved(6, 8, -40));
    }

    /// <summary>Nothing picked yet counts as before the first, so either arrow starts somewhere.</summary>
    [Fact]
    public void Nothing_picked_yet_starts_at_the_first()
    {
        Assert.Equal(0, _step.Moved(-1, 8, 1));
        Assert.Equal(0, _step.Moved(-1, 8, -1));
        Assert.Equal(0, _step.Moved(-40, 8, 1));
    }

    /// <summary>An empty shelf has nowhere to step to, and stays where it is.</summary>
    [Fact]
    public void An_empty_shelf_stays_put()
    {
        Assert.Equal(4, _step.Moved(4, 0, 1));
        Assert.Equal(4, _step.Moved(4, -1, -1));
    }

    /// <summary>Which side of the picker the pointer is on decides which step it offers.</summary>
    [Fact]
    public void The_side_decides_the_step()
    {
        Assert.Equal(PanelActions.PresetPrevious, _step.Side(10, 50));
        Assert.Equal(PanelActions.PresetNext, _step.Side(90, 50));
        Assert.Equal(PanelActions.PresetNext, _step.Side(50, 50));
    }

    /// <summary>A name is printed as a phrase, not as a run of capitalised words.</summary>
    [Fact]
    public void A_name_is_printed_as_a_phrase()
    {
        Assert.Equal("Low pass", _naming.Of("LowPass"));
        Assert.Equal("Band pass one pole", _naming.Of("BandPassOnePole"));
        Assert.Equal("Sine", _naming.Of("Sine"));
    }

    /// <summary>An acronym keeps its capitals, whatever case it arrives in.</summary>
    [Fact]
    public void An_acronym_keeps_its_capitals()
    {
        Assert.Equal("LFO", _naming.Of("LFO"));
        Assert.Equal("LFO", _naming.Of("lfo"));
        Assert.Equal("VCF", _naming.Of("Vcf"));
        Assert.Equal("PW", _naming.Of("pw"));
    }

    /// <summary>Nothing at all is nothing, rather than a throw.</summary>
    [Fact]
    public void Nothing_is_named_nothing()
    {
        Assert.Equal("", _naming.Of(null));
        Assert.Equal("", _naming.Of(""));
    }
}
