using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Config;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A settings file written before the pads joined the link layer, read by this build.
/// </summary>
/// <remarks>
/// The pads had a table of their own, and what somebody learned into it over a season is exactly
/// the sort of thing a rewrite loses quietly. It is carried over rather than migrated in place,
/// which is why the emptied table is the flag: an empty one has been carried, and a fresh
/// installation has an empty one already.
///
/// The one thing worth being exact about is the controller. A pad mapping never named one, since
/// the job ticked in SETTINGS already said which port was allowed to fire pads, so the links it
/// becomes name none either, which reads as any of them. Naming the port that happens to be
/// ticked today would be inventing a fact the old file never held.
/// </remarks>
public class PadMigrationTests
{
    /// <summary>A store pointed at a folder of its own, so no test reads another's settings.</summary>
    private static ConfigStore Store([System.Runtime.CompilerServices.CallerMemberName] string named = "") =>
        new("jinglebox2-pads-" + named);

    /// <summary>A settings file holding the old table and nothing pointed at anything.</summary>
    private static AppConfig Old()
    {
        var cfg = new AppConfig { Rows = 2, Columns = 2 };

        cfg.Midi.Pads = new List<MidiMapping>
        {
            new() { PadIndex = 0, Type = MidiMessageType.Note, Channel = 10, Value = 44 },
            new() { PadIndex = 1, Type = MidiMessageType.Note, Channel = 10, Value = 45 },
            new() { PadIndex = 2, Type = MidiMessageType.ControlChange, Channel = 1, Value = 20 },
            new() { PadIndex = 3, Type = MidiMessageType.Note, Channel = 10, Value = 47 }
        };

        return cfg;
    }

    /// <summary>Every row becomes a link on its own pad.</summary>
    [Fact]
    public void The_table_becomes_links()
    {
        var cfg = Old();

        Store().Save(cfg);

        var pads = cfg.Midi.Controls.Where(one => one.Kind == ControlKind.Pad).ToList();

        Assert.Equal(4, pads.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, pads.Select(one => one.Pad).OrderBy(one => one));
    }

    /// <summary>A row that sent a note is a link that answers a note.</summary>
    [Fact]
    public void A_note_row_stays_a_note()
    {
        var cfg = Old();

        Store().Save(cfg);

        var first = cfg.Midi.Controls.First(one => one.Kind == ControlKind.Pad && one.Pad == 0);

        Assert.Equal(MidiMessageType.Note, first.Sends);
        Assert.Equal(10, first.Channel);
        Assert.Equal(44, first.Cc);
    }

    /// <summary>And one that sent a controller stays a controller.</summary>
    [Fact]
    public void A_controller_row_stays_a_controller()
    {
        var cfg = Old();

        Store().Save(cfg);

        var third = cfg.Midi.Controls.First(one => one.Kind == ControlKind.Pad && one.Pad == 2);

        Assert.Equal(MidiMessageType.ControlChange, third.Sends);
        Assert.Equal(20, third.Cc);
    }

    /// <summary>It names no controller, because the table it came from named none.</summary>
    [Fact]
    public void The_links_answer_any_controller()
    {
        var cfg = Old();

        Store().Save(cfg);

        Assert.All(
            cfg.Midi.Controls.Where(one => one.Kind == ControlKind.Pad),
            one => Assert.Equal("", one.Device));
    }

    /// <summary>The table is emptied, which is what says it has been carried over.</summary>
    [Fact]
    public void The_old_table_is_emptied()
    {
        var cfg = Old();

        Store().Save(cfg);

        Assert.Empty(cfg.Midi.Pads);
    }

    /// <summary>Reading twice does not lay the same links down twice.</summary>
    [Fact]
    public void Carrying_it_over_happens_once()
    {
        var cfg = Old();
        var store = Store();

        store.Save(cfg);
        store.Save(cfg);

        Assert.Equal(4, cfg.Midi.Controls.Count(one => one.Kind == ControlKind.Pad));
    }

    /// <summary>A row for a pad the matrix has not got is carried over with the rest.</summary>
    /// <remarks>
    /// It fires nothing while the matrix is that size and comes back when it grows, which is the
    /// rule a link already keeps about a controller left in the other room.
    /// </remarks>
    [Fact]
    public void A_row_past_the_matrix_is_kept()
    {
        var cfg = Old();

        cfg.Midi.Pads.Add(new MidiMapping
        {
            PadIndex = 11, Type = MidiMessageType.Note, Channel = 10, Value = 55
        });

        Store().Save(cfg);

        Assert.Contains(cfg.Midi.Controls, one => one.Kind == ControlKind.Pad && one.Pad == 11);
    }

    /// <summary>A fresh installation starts with nothing pointed at the pads.</summary>
    /// <remarks>
    /// The table used to be filled in with notes 36 upwards whether or not anybody had asked,
    /// and the default layout has said the opposite since it was written: a pad nobody has
    /// pointed at should do nothing rather than something surprising.
    /// </remarks>
    [Fact]
    public void A_fresh_installation_has_no_pad_links()
    {
        var cfg = new AppConfig { Rows = 2, Columns = 2 };

        Store().Save(cfg);

        Assert.DoesNotContain(cfg.Midi.Controls, one => one.Kind == ControlKind.Pad);
        Assert.Empty(cfg.Midi.Pads);
    }

    /// <summary>What was carried over is in the file, not only in the object that was saved.</summary>
    [Fact]
    public void It_is_written_down()
    {
        var cfg = Old();
        var store = Store();

        store.Save(cfg);

        Assert.Contains("\"Pad\"", File.ReadAllText(store.ConfigPath));
    }
}
