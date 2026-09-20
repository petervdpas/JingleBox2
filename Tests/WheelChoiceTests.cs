using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which control the modulation wheel turns, and the three layers that answer it.
/// </summary>
/// <remarks>
/// The machine names a control in its own manifest so its wheel arrives working on somebody
/// else's computer; an instrument may name another for this part, which travels with its preset
/// and its song; and a link somebody pointed at the wheel beats both, which happens further out
/// and is not this file's subject.
///
/// The rule is asked directly, which is what <see cref="WheelChoice"/> exists for: written
/// inside whatever wanted it, the whole design could only be read out of two classes and could
/// only be tested through a window.
/// </remarks>
public class WheelChoiceTests
{
    /// <summary>What the machine says, where the instrument has said nothing.</summary>
    [Fact]
    public void An_instrument_that_says_nothing_plays_the_machines_choice() =>
        Assert.Equal("vib_depth", Choice.Turns(Instrument(""), Machine("vib_depth")));

    /// <summary>And the instrument's own choice beats it.</summary>
    [Fact]
    public void An_instruments_own_choice_beats_the_machines() =>
        Assert.Equal("cutoff", Choice.Turns(Instrument("cutoff"), Machine("vib_depth")));

    /// <summary>
    /// An instrument can name one where the machine named none.
    /// </summary>
    /// <remarks>
    /// The layers are a default and an exception rather than a permission: a machine saying
    /// nothing is a machine with no opinion, which is not the same as a machine refusing.
    /// </remarks>
    [Fact]
    public void An_instrument_can_name_one_where_the_machine_named_none() =>
        Assert.Equal("cutoff", Choice.Turns(Instrument("cutoff"), Machine("")));

    /// <summary>With neither saying anything the wheel turns nothing at all.</summary>
    /// <remarks>
    /// Which is a machine that has not thought about its wheel, and is most of them: the pitch
    /// wheel still bends every engine here, and the modulation wheel has nowhere to go.
    /// </remarks>
    [Fact]
    public void With_neither_saying_anything_it_turns_nothing()
    {
        Assert.Equal("", Choice.Turns(Instrument(""), Machine("")));
        Assert.Equal("", Choice.Turns(Instrument(""), null));
        Assert.Equal("", Choice.Turns(null, null));
    }

    /// <summary>With no instrument at all the machine's own declaration stands.</summary>
    /// <remarks>
    /// There is nothing to say otherwise, which is what the layer under it is for. It is the
    /// rack's keyboard on a machine nobody has made an instrument of yet, and it is why the
    /// question is asked of the pair rather than of whichever one happens to be in hand.
    /// </remarks>
    [Fact]
    public void With_no_instrument_the_machine_still_decides() =>
        Assert.Equal("vib_depth", Choice.Turns(null, Machine("vib_depth")));

    /// <summary>
    /// An instrument on a machine this installation has not got still says what it wants.
    /// </summary>
    /// <remarks>
    /// Nothing sounds there, since the machine is not here to sound it, and the key is carried
    /// on the instrument rather than worked out from the machine: a song opened on a computer
    /// that has since been given the machine plays the wheel the part was written with.
    /// </remarks>
    [Fact]
    public void An_instrument_keeps_its_choice_with_no_machine_behind_it() =>
        Assert.Equal("cutoff", Choice.Turns(Instrument("cutoff"), null));

    /// <summary>The rule, which needs no song, no window and no machine on a disc.</summary>
    private static readonly IWheelChoice Choice = new WheelChoice();

    /// <summary>The picker's first row is the machine's own, and it is what empty reads back as.</summary>
    [Fact]
    public void The_first_row_is_the_machines_own()
    {
        var instrument = Instrument("");
        var choice = new InstrumentWheel(instrument, () => Machine("vib_depth"));

        Assert.Equal(InstrumentWheel.AsTheMachineSays, choice.Names[0]);
        Assert.Equal(new[] { InstrumentWheel.AsTheMachineSays, "Vibrato depth", "Cutoff" }, choice.Names);
        Assert.Equal(0, choice.Picked);
    }

    /// <summary>
    /// The rows are the controls on the machine's face, in the order it draws them.
    /// </summary>
    /// <remarks>
    /// Both halves matter and both were wrong first. The order a manifest declares its
    /// parameters in is not an order anybody sees, so the list came out in an order that matched
    /// nothing on the screen; and a parameter no control draws is plumbing rather than a choice,
    /// so offering it is offering something nobody can watch move. It is the same walk the
    /// automation picker makes, which had both answers already.
    /// </remarks>
    [Fact]
    public void The_rows_are_the_face_and_not_the_manifest()
    {
        var choice = new InstrumentWheel(Instrument(""), () => Machine(""));

        Assert.Equal(
            new[] { InstrumentWheel.AsTheMachineSays, "Vibrato depth", "Cutoff" },
            choice.Names);

        Assert.DoesNotContain("Not on the face", choice.Names);
    }

    /// <summary>Picking a control writes its key, and picking the first row takes it off again.</summary>
    [Fact]
    public void Picking_writes_the_key_and_the_first_row_clears_it()
    {
        var instrument = Instrument("");
        int said = 0;
        var choice = new InstrumentWheel(instrument, () => Machine("vib_depth"), () => said++);

        choice.Picked = 2;

        Assert.Equal("cutoff", instrument.WheelKey);
        Assert.Equal(2, choice.Picked);
        Assert.Equal(1, said);

        choice.Picked = 0;

        Assert.Equal("", instrument.WheelKey);
        Assert.Equal(0, choice.Picked);
        Assert.Equal(2, said);
    }

    /// <summary>
    /// Picking what is already picked says nothing, and a key the machine has not got shows nothing.
    /// </summary>
    /// <remarks>
    /// The first half is what keeps a panel being drawn from writing its own first row into
    /// somebody's song: putting a value into a control raises what a hand on it raises. The
    /// second is the picker refusing to point at the wrong row, which is the same answer the
    /// sound gives.
    /// </remarks>
    [Fact]
    public void Nothing_is_said_for_a_choice_that_did_not_move()
    {
        var instrument = Instrument("cutoff");
        int said = 0;
        var choice = new InstrumentWheel(instrument, () => Machine("vib_depth"), () => said++);

        choice.Picked = 2;

        Assert.Equal(0, said);

        instrument.WheelKey = "no_such_control";

        Assert.Equal(-1, choice.Picked);
    }

    /// <summary>With no machine behind it the picker offers the one row that is not a control.</summary>
    [Fact]
    public void With_no_machine_there_is_nothing_to_offer()
    {
        var choice = new InstrumentWheel(Instrument(""), () => null);

        Assert.Equal(new[] { InstrumentWheel.AsTheMachineSays }, choice.Names);
        Assert.Equal(0, choice.Picked);
    }

    /// <summary>It travels with a preset, beside the bend range and the new note action.</summary>
    [Fact]
    public void It_travels_with_the_sound()
    {
        var from = Instrument("cutoff");
        var onto = Instrument("");

        onto.TakeSoundFrom(from);

        Assert.Equal("cutoff", onto.WheelKey);
        Assert.Equal("cutoff", from.Clone().WheelKey);
    }

    /// <summary>
    /// The Menu offers one row per control under one line, and marks the one in force.
    /// </summary>
    /// <remarks>
    /// One line that opens onto the list rather than the list itself, because a device's
    /// controls run to twenty five and that many on the top of a Menu is a wall with whatever
    /// somebody opened it for three screens down.
    /// </remarks>
    [Fact]
    public void The_menu_offers_the_controls_under_one_line()
    {
        var instrument = Instrument("cutoff");
        var lines = new WheelMenu(new InstrumentWheel(instrument, () => Machine("vib_depth"))).Read();

        var line = Assert.Single(lines);

        Assert.Equal(WheelMenu.Words, line.Said);
        Assert.True(line.Live);
        Assert.Equal(MenuOptionWords.Wheel, line.Option);

        Assert.Equal(
            new[] { InstrumentWheel.AsTheMachineSays, "Vibrato depth", "Cutoff" },
            line.Lines.Select(one => one.Said));

        Assert.Equal(new bool?[] { false, false, true }, line.Lines.Select(one => one.Ticked));
    }

    /// <summary>And picking one of those rows is what writes the key.</summary>
    [Fact]
    public void Picking_a_row_on_the_menu_moves_the_wheel()
    {
        var instrument = Instrument("");
        var lines = new WheelMenu(new InstrumentWheel(instrument, () => Machine("vib_depth"))).Read();

        lines[0].Lines[2].Chosen!();

        Assert.Equal("cutoff", instrument.WheelKey);
    }

    /// <summary>
    /// A device with nothing a wheel could turn keeps the line and loses the press.
    /// </summary>
    /// <remarks>
    /// The rule the help line already keeps: a line that is not there says the host cannot do
    /// it, and a grey one says this device has nothing to offer. That is a plugin, which owns
    /// its own wheel and is not pointed at from here.
    /// </remarks>
    [Fact]
    public void A_device_with_nothing_to_turn_keeps_the_line_and_loses_the_press()
    {
        var line = Assert.Single(new WheelMenu(new InstrumentWheel(Instrument(""), () => null)).Read());

        Assert.False(line.Live);
        Assert.Empty(line.Lines);
    }

    /// <summary>
    /// A machine that names no options carries the new one, which is every machine that ships.
    /// </summary>
    /// <remarks>
    /// The rule an option added later rests on, and the whole reason none of the shipped faces
    /// had to be edited for this: a Menu naming no options carries all of them.
    /// </remarks>
    [Fact]
    public void An_option_added_later_reaches_every_shipped_machine()
    {
        Assert.Contains(MenuOptionWords.Wheel, MenuOptionWords.All);

        foreach (string path in Directory.GetFiles(
                     Path.Combine(Root(), "rack", "machines"), "machine.json", SearchOption.AllDirectories))
        {
            using var file = JsonDocument.Parse(File.ReadAllText(path));

            if (Menu(file.RootElement.GetProperty("Panel").GetProperty("Root")) is not { } part) continue;

            Assert.False(
                part.TryGetProperty("Properties", out var properties)
                && properties.TryGetProperty(MenuOptionWords.Property, out _),
                path + " names its menu options, so it will not carry one added later");
        }
    }

    /// <summary>The Menu part on a face, wherever it was dropped.</summary>
    private static JsonElement? Menu(JsonElement element)
    {
        if (element.TryGetProperty("Element", out var kind) && kind.GetString() == "Menu") return element;

        if (!element.TryGetProperty("Children", out var children)) return null;

        foreach (var child in children.EnumerateArray())
        {
            if (Menu(child) is { } found) return found;
        }

        return null;
    }

    /// <summary>
    /// A machine with two controls a wheel could turn, naming one of them or none.
    /// </summary>
    /// <remarks>
    /// It has a face as well as a list of parameters, since what the rows are is read off the
    /// face: a parameter no control draws is plumbing rather than a choice, and the order a
    /// manifest happens to declare them in is not an order anybody sees. The knobs are drawn in
    /// the opposite order to the declarations for exactly that reason, so a list taken off the
    /// wrong one comes out backwards rather than merely unproven.
    /// </remarks>
    private static SoundMachineProject Machine(string wheel) => new()
    {
        Id = MachineId,
        Name = "Test",
        Engine = "synth",
        Wheel = wheel,
        Parameters =
        [
            new Parameter { Key = "cutoff", Name = "Cutoff", Min = 0, Max = 1 },
            new Parameter { Key = "vib_depth", Name = "Vibrato depth", Min = 0, Max = 1 },
            new Parameter { Key = "hidden", Name = "Not on the face", Min = 0, Max = 1 }
        ],
        Panel = new Panel
        {
            Root = new PanelElement
            {
                Element = ElementKinds.Grid,
                Children =
                [
                    new PanelElement { Element = ElementKinds.Knob, Parameter = "vib_depth" },
                    new PanelElement { Element = ElementKinds.Knob, Parameter = "cutoff" }
                ]
            }
        }
    };

    /// <summary>What the test machine is called, which is what an instrument names to be on it.</summary>
    private const string MachineId = "test.wheel";

    /// <summary>An instrument on that machine, saying what its own wheel turns or saying nothing.</summary>
    private static TrackerInstrument Instrument(string wheel) => new()
    {
        Name = "One",
        Kind = TrackerInstrumentKind.Synth,
        MachineId = MachineId,
        WheelKey = wheel
    };

    /// <summary>The checkout this is running out of, for the machines that ship in it.</summary>
    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack"))) at = at.Parent;

        return at?.FullName ?? AppContext.BaseDirectory;
    }
}
