using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A template written out, and read back on somebody else's machine.
/// </summary>
/// <remarks>
/// The point of a template is that it leaves this computer, so what is worth testing is not
/// that a file can be written but that everything in it still means the same thing when it
/// arrives: the machine named by its id, the parameter by the machine's own key, the strip by
/// its number, and the controller by what a profile calls it rather than by the port name,
/// which is spelled differently on every system.
///
/// All of it runs with no controller, no window and no disc except a temporary folder, which is
/// the whole reason the naming and the document were kept apart from the profiles.
/// </remarks>
public class ControlTemplateTests
{
    /// <summary>The rule the page and the file both read.</summary>
    private static readonly ILinkTargets Targets = new LinkTargets();

    /// <summary>Reading and writing, with the real folder and the real whole-file write.</summary>
    private static readonly IControlTemplates Templates = new ControlTemplates(Targets);

    /// <summary>A knob on a machine, as the panel writes one down.</summary>
    /// <param name="key">Which parameter.</param>
    /// <param name="cc">Which controller number.</param>
    private static ControlMapping OnMachine(string key, int cc) => new()
    {
        Kind = ControlKind.SoundDevice,
        Machine = "machine.oddskilla",
        Key = key,
        Owner = "OddSkilla",
        Name = "OddSkilla " + key,
        Device = "nanoKONTROL2 _ CTRL",
        Channel = 1,
        Cc = cc,
        Pickup = ControlPickup.Takeover
    };

    /// <summary>A file of our own to write into, gone with the folder when the run ends.</summary>
    /// <param name="named">What to call it.</param>
    private static string Somewhere(string named) =>
        Path.Combine(Templates.Folder(), named + "." + ControlTemplates.Extension);

    /// <summary>
    /// What a device template says, in the words a person reads in the file.
    /// </summary>
    /// <remarks>
    /// The word is device and not machine, since a soundmachine and an effect are the same thing
    /// to a link: an id and the key of the control it was pointed at. A file already on somebody's
    /// disc says machine and is still read, which the test below says.
    /// </remarks>
    [Fact]
    public void A_device_is_named_by_its_id_and_its_own_keys()
    {
        var template = Templates.Describe("nanoKONTROL2", new[] { OnMachine("attack", 0) });

        Assert.NotNull(template);
        Assert.Equal("nanoKONTROL2", template!.Controller);
        Assert.Equal("sounddevice", template.Target.Kind);
        Assert.Equal("machine.oddskilla", template.Target.Id);
        Assert.Equal("OddSkilla", template.Target.Name);
        Assert.Equal("attack", template.Controls[0].Parameter);
        Assert.Equal(0, template.Controls[0].Cc);
    }

    /// <summary>The whole way round: links, file, links, and nothing lost on the way.</summary>
    [Fact]
    public void A_template_goes_out_and_comes_back_the_same()
    {
        var made = new[] { OnMachine("attack", 0), OnMachine("decay", 1), OnMachine("duty", 16) };

        string path = Somewhere("round-trip");

        Templates.Write(path, Templates.Describe("nanoKONTROL2", made)!);

        var reading = Templates.Take(Templates.Open(path), new[] { "nanoKONTROL2 _ CTRL" }, _ => "nanoKONTROL2");

        Assert.Equal(0, reading.Skipped);
        Assert.Equal(3, reading.Links.Count);

        foreach (var (was, now) in made.Zip(reading.Links.OrderBy(one => one.Cc)))
        {
            Assert.Equal(was.Machine, now.Machine);
            Assert.Equal(was.Key, now.Key);
            Assert.Equal(was.Cc, now.Cc);
            Assert.Equal(was.Channel, now.Channel);
            Assert.Equal(was.Pickup, now.Pickup);
            Assert.Equal(was.Owner, now.Owner);
            Assert.Equal(was.Name, now.Name);
        }
    }

    /// <summary>
    /// The port is worked out from what a profile calls it, which is the one thing that cannot
    /// travel.
    /// </summary>
    /// <remarks>
    /// The same device is <c>nanoKONTROL2 _ CTRL</c> to the ALSA sequencer and
    /// <c>nanoKONTROL2 _ SLIDER/KNOB</c> to rawmidi, and Windows spells it a third way.
    /// </remarks>
    [Fact]
    public void The_controller_is_found_by_its_profiles_name_and_not_by_the_port()
    {
        var reading = Templates.Take(
            Templates.Describe("nanoKONTROL2", new[] { OnMachine("attack", 0) }),
            new[] { "Midi Through Port-0", "nanoKONTROL2 _ SLIDER/KNOB" },
            port => port.Contains("nanoKONTROL2") ? "nanoKONTROL2" : port);

        Assert.True(reading.Found);
        Assert.Equal("nanoKONTROL2 _ SLIDER/KNOB", reading.Links[0].Device);
    }

    /// <summary>
    /// A controller that is not plugged in keeps the name the file carried.
    /// </summary>
    /// <remarks>
    /// The links are laid down anyway and wait for it, which is the same rule a link already
    /// kept: a controller left in the other room is not a decision to unwire it. Refusing the
    /// import for a cable would be the worse answer, and telling nobody would be worse still,
    /// which is why the reading says whether it found one.
    /// </remarks>
    [Fact]
    public void A_controller_that_is_not_here_still_takes_its_template()
    {
        var reading = Templates.Take(
            Templates.Describe("nanoKONTROL2", new[] { OnMachine("attack", 0) }),
            new[] { "Midi Through Port-0" },
            port => port);

        Assert.False(reading.Found);
        Assert.Single(reading.Links);
        Assert.Equal("nanoKONTROL2", reading.Links[0].Device);
    }

    /// <summary>
    /// A mixer template is the whole desk, and each line says which strip it is on.
    /// </summary>
    /// <remarks>
    /// The mixer is one thing to point a controller at, so the target says only that it is the
    /// mixer. A strip is written the way the mixer says it, the master being the word and a track
    /// its number counting from one, because a file read by people should say what the screen
    /// says.
    /// </remarks>
    [Fact]
    public void A_mixer_template_is_the_whole_desk_and_each_line_names_its_strip()
    {
        var desk = Templates.Describe("nanoKONTROL2", new[]
        {
            Fader(MixControl.Volume, 2, 0),
            Fader(MixControl.Pan, JingleBox2.Tracker.TrackerPlayer.MasterStrip, 7)
        })!;

        Assert.Equal("mixer", desk.Target.Kind);
        Assert.Equal("", desk.Target.Id);
        Assert.Equal("Mixer", desk.Target.Name);

        Assert.Equal("3", desk.Controls[0].Strip);
        Assert.Equal("level", desk.Controls[0].Parameter);
        Assert.Equal("master", desk.Controls[1].Strip);
        Assert.Equal("pan", desk.Controls[1].Parameter);

        var back = Templates.Take(desk).Links;

        Assert.Equal(2, back[0].Track);
        Assert.Equal(JingleBox2.Tracker.TrackerPlayer.MasterStrip, back[1].Track);
    }

    /// <summary>
    /// A mixer template written before the strip moved onto the line still reads.
    /// </summary>
    /// <remarks>
    /// Those named their one strip in the target, since a card was a strip. A file on somebody's
    /// disc outlives a decision about how cards are cut, so the target is still read where a line
    /// says nothing.
    /// </remarks>
    [Fact]
    public void A_mixer_template_that_names_its_strip_in_the_target_still_reads()
    {
        var older = Templates.Describe("nanoKONTROL2", new[] { Fader(MixControl.Volume, 2, 0) })!;

        older.Target.Id = "3";
        older.Controls[0].Strip = "";

        Assert.Equal(2, Templates.Take(older).Links[0].Track);
    }

    /// <summary>A fader on a strip, as the mixer writes one down.</summary>
    /// <param name="what">Which of the strip's controls.</param>
    /// <param name="track">Which strip.</param>
    /// <param name="cc">Which controller number.</param>
    private static ControlMapping Fader(MixControl what, int track, int cc)
    {
        var one = MixLinks.On(what, track);

        one.Device = "nanoKONTROL2 _ CTRL";
        one.Cc = cc;

        return one;
    }

    /// <summary>The transport is one target and its keys are words.</summary>
    [Fact]
    public void The_transport_is_written_by_key()
    {
        var template = Templates.Describe("nanoKONTROL2", new[] { TransportLinks.For(TransportKey.Play) })!;

        Assert.Equal("transport", template.Target.Kind);
        Assert.Equal("play", template.Controls[0].Parameter);
        Assert.Equal(TransportKey.Play, Templates.Take(template).Links[0].Transport);
    }

    /// <summary>
    /// Links on two things are refused rather than half written.
    /// </summary>
    /// <remarks>
    /// A template is one controller against one target. A file holding two would be a file
    /// whose own heading is a lie, and whoever opened it would get one of them.
    /// </remarks>
    [Fact]
    public void Links_on_two_targets_are_not_one_template()
    {
        var mixed = new[] { OnMachine("attack", 0), Fader(MixControl.Volume, 0, 1) };

        Assert.Null(Templates.Describe("nanoKONTROL2", mixed));
    }

    /// <summary>
    /// A line this build has no word for is left out and counted.
    /// </summary>
    /// <remarks>
    /// Which is what a template from a newer version looks like. The useful answer is the part
    /// that works plus a line saying how much did not, since refusing the file would throw away
    /// the nine controls that were fine.
    /// </remarks>
    [Fact]
    public void What_cannot_be_read_is_left_out_and_said()
    {
        var template = Templates.Describe("nanoKONTROL2", new[] { OnMachine("attack", 0), OnMachine("decay", 1) })!;

        template.Controls[1].Parameter = "";

        var reading = Templates.Take(template);

        Assert.Single(reading.Links);
        Assert.Equal(1, reading.Skipped);
    }

    /// <summary>A file that is not one of these opens as nothing rather than throwing.</summary>
    [Fact]
    public void Something_that_is_not_a_template_is_not_read_as_one()
    {
        string path = Somewhere("not-a-template");

        File.WriteAllText(path, "{ \"jinglebox\": \"machine\", \"name\": \"OddSkilla\" }");

        Assert.Null(Templates.Open(path));
        Assert.Null(Templates.Open(Path.Combine(Templates.Folder(), "no-such-file.jbtl")));
    }

    /// <summary>Nonsense is nonsense, and it comes back as nothing rather than as a crash.</summary>
    [Fact]
    public void A_damaged_file_is_not_read_as_a_template()
    {
        string path = Somewhere("damaged");

        File.WriteAllText(path, "{ \"jinglebox\": \"control-temp");

        Assert.Null(Templates.Open(path));
    }

    /// <summary>What the file is called, from what is in it.</summary>
    [Fact]
    public void A_template_suggests_a_name_a_person_would_recognise()
    {
        Assert.Equal(
            "nanokontrol2-oddskilla",
            Templates.FileName(Templates.Describe("nanoKONTROL2", new[] { OnMachine("attack", 0) })!));
    }

    /// <summary>
    /// Taking the same template twice leaves what one did.
    /// </summary>
    /// <remarks>
    /// One control does one job, so an arriving link displaces whatever held its control. That
    /// rule is what makes an import safe to repeat, and it is the same rule a link made by hand
    /// keeps rather than a second one written for importing.
    /// </remarks>
    [Fact]
    public void Importing_the_same_template_twice_does_not_pile_up()
    {
        var desk = new List<ControlMapping>();
        var link = new ControlLink(desk, () => { });

        var template = Templates.Describe("nanoKONTROL2", new[] { OnMachine("attack", 0), OnMachine("decay", 1) })!;

        link.Take(Read(template));
        Assert.Equal(2, desk.Count);

        link.Take(Read(template));
        Assert.Equal(2, desk.Count);
    }

    /// <summary>A template read as though the controller it names were plugged in here.</summary>
    /// <param name="template">What to read.</param>
    private static IReadOnlyList<ControlMapping> Read(ControlTemplate template) =>
        Templates.Take(template, new[] { "nanoKONTROL2 _ CTRL" }, _ => "nanoKONTROL2").Links;

    /// <summary>And pointing the same knob somewhere else replaces, as it always did.</summary>
    [Fact]
    public void A_template_displaces_what_held_the_same_knob()
    {
        var desk = new List<ControlMapping> { OnMachine("release", 0) };
        var link = new ControlLink(desk, () => { });

        link.Take(Templates.Take(
            Templates.Describe("nanoKONTROL2", new[] { OnMachine("attack", 0) })!,
            new[] { "nanoKONTROL2 _ CTRL" },
            _ => "nanoKONTROL2").Links);

        Assert.Single(desk);
        Assert.Equal("attack", desk[0].Key);
    }
}

/// <summary>
/// The page's own half of it: export from a card, import back into an empty layer.
/// </summary>
/// <remarks>
/// Apart from the reading and writing above, because this is the part that goes through the
/// list on the screen: which section is written out, what the file is called, and whether the
/// cards come back afterwards. The file picker is the window's and is not here; everything
/// either button does once a path is known is.
/// </remarks>
public class ControlLinksPageTests
{
    /// <summary>A knob on a machine, as the panel writes one down.</summary>
    /// <param name="key">Which parameter.</param>
    /// <param name="cc">Which controller number.</param>
    private static ControlMapping OnMachine(string key, int cc) => new()
    {
        Kind = ControlKind.SoundDevice,
        Machine = "machine.oddskilla",
        Key = key,
        Owner = "OddSkilla",
        Name = "OddSkilla " + key,
        Device = "nanoKONTROL2 _ CTRL",
        Channel = 1,
        Cc = cc
    };

    /// <summary>A desk with two links on one machine, and the page over it.</summary>
    /// <param name="desk">The links, kept so a test can count them.</param>
    private static ControlLinksViewModel Page(out List<ControlMapping> desk)
    {
        desk = new List<ControlMapping> { OnMachine("attack", 0), OnMachine("decay", 1) };

        return new ControlLinksViewModel(
            new ControlLink(desk, () => { }),
            ports: () => new[] { "nanoKONTROL2 _ CTRL" });
    }

    /// <summary>
    /// Out of the card it is drawn from, and back into an empty layer.
    /// </summary>
    /// <remarks>
    /// The list is read again by hand between the acts and the checks. In the application it
    /// reads itself, because <see cref="ControlLink.Say"/> raises Changed on the drawing thread;
    /// here there is no drawing thread pumping, so whether the rebuild has happened by the next
    /// line depends on what ran before this test in the assembly. Asking for it is the same
    /// rebuild, taken at a moment this test decides rather than one it hopes for.
    /// </remarks>
    [Fact]
    public void A_card_is_written_out_and_read_back_in()
    {
        var page = Page(out var desk);

        var card = Assert.Single(page.Cards);

        string path = Path.Combine(new ControlTemplates().Folder(), page.Suggest(card) + ".jbtl");

        page.Export(card, path);

        Assert.True(File.Exists(path));

        page.ForgetAllCommand.Execute(null);
        page.Reread();

        Assert.Empty(desk);
        Assert.Empty(page.Cards);

        page.Import(path);
        page.Reread();

        Assert.Equal(2, desk.Count);
        Assert.Equal("OddSkilla", Assert.Single(page.Cards).Title);
        Assert.Contains("2 controls for OddSkilla", page.Status);
    }

    /// <summary>Picking the wrong file says so rather than doing nothing.</summary>
    [Fact]
    public void A_file_that_is_not_a_template_says_so()
    {
        var page = Page(out _);

        string path = Path.Combine(new ControlTemplates().Folder(), "wrong.jbtl");

        File.WriteAllText(path, "not json at all");

        page.Import(path);

        Assert.Contains("is not a control template", page.Status);
    }

    /// <summary>Cards arrive folded away, and the one that is opened stays open.</summary>
    /// <remarks>
    /// The list is thrown away and made afresh whenever anything moves, so without the open
    /// card being remembered by its key, laying one link down would fold up the card somebody
    /// is working in.
    /// </remarks>
    [Fact]
    public void The_open_card_survives_the_list_being_rebuilt()
    {
        var page = Page(out _);

        var card = Assert.Single(page.Cards);

        Assert.False(card.Open);

        card.Open = true;

        page.Reread();

        Assert.True(Assert.Single(page.Cards).Open);
    }

    /// <summary>Opening one card folds the others away.</summary>
    /// <remarks>
    /// Two machines and one controller is two cards. Opening the second must fold the first,
    /// and the folding must not clear the key of the card that has just been opened, which is
    /// what the guard in <c>Folded</c> is there for.
    /// </remarks>
    [Fact]
    public void Only_one_card_is_open_at_a_time()
    {
        var desk = new List<ControlMapping>
        {
            OnMachine("attack", 0),
            new()
            {
                Kind = ControlKind.SoundDevice,
                Machine = "machine.ouroboros",
                Key = "tune",
                Owner = "Ouroboros",
                Name = "Ouroboros tune",
                Device = "nanoKONTROL2 _ CTRL",
                Channel = 1,
                Cc = 16
            }
        };

        var page = new ControlLinksViewModel(new ControlLink(desk, () => { }));

        Assert.Equal(2, page.Cards.Count);

        page.Cards[0].Open = true;

        Assert.True(page.Cards[0].Open);
        Assert.False(page.Cards[1].Open);

        page.Cards[1].Open = true;

        Assert.False(page.Cards[0].Open);
        Assert.True(page.Cards[1].Open);

        page.Reread();

        Assert.False(page.Cards[0].Open);
        Assert.True(page.Cards[1].Open);
    }
}
