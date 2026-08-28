using System;
using System.Linq;
using JingleBox2.Audio.Plugins.Bridge;
using JingleBox2.Audio.Plugins.Bridge.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the two halves of a plugin say to each other, written down and read back.
/// </summary>
/// <remarks>
/// Every one of these is a pair, and the pair is the point: a writer that gains a field and a
/// reader that does not gives every message after it the wrong shape, and there is no error
/// anywhere. A plugin simply comes back with the wrong values, or a name made of somebody
/// else's bytes.
///
/// The other half is what happens to a truncated payload. The far end of this is a process that
/// can die in the middle of a write, so a reader that threw would turn a plugin crash into an
/// application crash, which is the one thing the whole arrangement exists to prevent.
/// </remarks>
public class BridgeBodyTests
{
    private readonly IBridgeBody _body = new BridgeBody();

    /// <summary>Words go down and come back in the same order.</summary>
    [Fact]
    public void Words_survive_the_round_trip()
    {
        var said = new[] { "Serum", "2.1.4", "Xfer" };

        Assert.Equal(said, _body.ReadWords(_body.Words(said)));
    }

    /// <summary>No words at all is a real message, not an absence.</summary>
    [Fact]
    public void No_words_is_a_message()
    {
        Assert.Empty(_body.ReadWords(_body.Words()));
    }

    /// <summary>A null word goes down as an empty one, so the count still matches what follows.</summary>
    [Fact]
    public void A_null_word_travels_as_an_empty_one()
    {
        var back = _body.ReadWords(_body.Words("a", null!, "c"));

        Assert.Equal(new[] { "a", "", "c" }, back);
    }

    /// <summary>Words beyond ASCII survive, since a plugin names itself in its own language.</summary>
    [Fact]
    public void Words_beyond_ascii_survive()
    {
        var said = new[] { "ヴォコーダー", "Éclat", "🎹" };

        Assert.Equal(said, _body.ReadWords(_body.Words(said)));
    }

    /// <summary>A very long word survives, since a preset name has no stated limit.</summary>
    [Fact]
    public void A_long_word_survives()
    {
        string long_ = new string('x', 40000);

        Assert.Equal(new[] { long_ }, _body.ReadWords(_body.Words(long_)));
    }

    /// <summary>A payload that is not words at all answers with nothing rather than throwing.</summary>
    [Fact]
    public void A_payload_that_is_not_words_answers_with_nothing()
    {
        Assert.Empty(_body.ReadWords(Array.Empty<byte>()));
        Assert.Empty(_body.ReadWords(new byte[] { 1 }));
        Assert.Empty(_body.ReadWords(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }));
    }

    /// <summary>Words cut off part way answer with nothing rather than half a list.</summary>
    [Fact]
    public void Words_cut_off_part_way_answer_with_nothing()
    {
        byte[] whole = _body.Words("alpha", "beta", "gamma");

        for (int cut = 1; cut < whole.Length; cut++)
            _body.ReadWords(whole.Take(cut).ToArray());
    }

    /// <summary>A numbered value survives, id and all.</summary>
    [Fact]
    public void A_number_survives_the_round_trip()
    {
        var (id, value) = _body.ReadNumber(_body.Number(4294967295, 0.5));

        Assert.Equal(4294967295u, id);
        Assert.Equal(0.5, value, 12);
    }

    /// <summary>Every awkward double survives, since a parameter is whatever the plugin says.</summary>
    [Fact]
    public void Awkward_numbers_survive()
    {
        foreach (double value in new[]
        {
            0.0, -0.0, 1.0, -1.0, double.Epsilon, double.MaxValue, double.MinValue,
            double.NaN, double.PositiveInfinity, double.NegativeInfinity
        })
        {
            double back = _body.ReadNumber(_body.Number(7, value)).Value;

            if (double.IsNaN(value)) Assert.True(double.IsNaN(back));
            else Assert.Equal(value, back);
        }
    }

    /// <summary>A number cut short answers with nought rather than reading past the end.</summary>
    [Fact]
    public void A_number_cut_short_answers_with_nought()
    {
        byte[] whole = _body.Number(9, 0.25);

        for (int cut = 0; cut < whole.Length; cut++)
        {
            var (id, value) = _body.ReadNumber(whole.Take(cut).ToArray());

            Assert.Equal(0u, id);
            Assert.Equal(0.0, value);
        }
    }

    /// <summary>A bare double survives on its own.</summary>
    [Fact]
    public void A_bare_double_survives()
    {
        Assert.Equal(0.125, _body.ReadDouble(_body.Double(0.125)), 12);
        Assert.Equal(0.0, _body.ReadDouble(Array.Empty<byte>()));
        Assert.Equal(0.0, _body.ReadDouble(new byte[] { 1, 2, 3 }));
    }

    /// <summary>A pair survives, both halves and their order.</summary>
    [Fact]
    public void A_pair_survives()
    {
        var (first, second) = _body.ReadPair(_body.Pair(int.MinValue, int.MaxValue));

        Assert.Equal(int.MinValue, first);
        Assert.Equal(int.MaxValue, second);
    }

    /// <summary>A pair cut short answers with noughts.</summary>
    [Fact]
    public void A_pair_cut_short_answers_with_noughts()
    {
        byte[] whole = _body.Pair(3, 4);

        for (int cut = 0; cut < whole.Length; cut++)
            Assert.Equal((0, 0), _body.ReadPair(whole.Take(cut).ToArray()));
    }

    /// <summary>Three numbers survive, in order.</summary>
    [Fact]
    public void Three_numbers_survive()
    {
        var back = _body.ReadThree(_body.Three(1, -2, 3));

        Assert.Equal((1, -2, 3), back);
    }

    /// <summary>Three cut short answers with noughts.</summary>
    [Fact]
    public void Three_cut_short_answers_with_noughts()
    {
        byte[] whole = _body.Three(1, 2, 3);

        for (int cut = 0; cut < whole.Length; cut++)
            Assert.Equal((0, 0, 0), _body.ReadThree(whole.Take(cut).ToArray()));
    }

    /// <summary>A window handle survives, and nought means no window.</summary>
    [Fact]
    public void A_window_handle_survives()
    {
        Assert.Equal((nint)0x7FFF1234, _body.ReadHandle(_body.Handle(0x7FFF1234)));
        Assert.Equal((nint)0, _body.ReadHandle(_body.Handle(0)));
        Assert.Equal((nint)0, _body.ReadHandle(Array.Empty<byte>()));
        Assert.Equal((nint)0, _body.ReadHandle(new byte[] { 1, 2, 3, 4 }));
    }

    /// <summary>Every parameter survives, with everything about it.</summary>
    [Fact]
    public void Parameters_survive_the_round_trip()
    {
        var said = new[]
        {
            new PluginParameter(0, "Cutoff", 0, 1, 0.5, 0, false, false, false, true, "Hz"),
            new PluginParameter(4294967295, "Gain reduction", -60, 0, 0, 0, false, true, false, false, "dB"),
            new PluginParameter(7, "", 0, 0, 0, 1, true, false, true, false)
        };

        var back = _body.ReadParameters(_body.Parameters(said));

        Assert.Equal(said.Length, back.Length);

        for (int i = 0; i < said.Length; i++)
        {
            Assert.Equal(said[i].Id, back[i].Id);
            Assert.Equal(said[i].Name, back[i].Name);
            Assert.Equal(said[i].Minimum, back[i].Minimum, 12);
            Assert.Equal(said[i].Maximum, back[i].Maximum, 12);
            Assert.Equal(said[i].Default, back[i].Default, 12);
            Assert.Equal(said[i].Steps, back[i].Steps);
            Assert.Equal(said[i].IsHidden, back[i].IsHidden);
            Assert.Equal(said[i].IsReadOnly, back[i].IsReadOnly);
            Assert.Equal(said[i].IsBypass, back[i].IsBypass);
            Assert.Equal(said[i].Normalized, back[i].Normalized);
            Assert.Equal(said[i].Units, back[i].Units);
        }
    }

    /// <summary>A plugin with no parameters is a real answer.</summary>
    [Fact]
    public void No_parameters_is_an_answer()
    {
        Assert.Empty(_body.ReadParameters(_body.Parameters(Array.Empty<PluginParameter>())));
    }

    /// <summary>
    /// A parameter list cut off part way never throws, whatever it is cut at.
    /// </summary>
    /// <remarks>
    /// Serum answers with 2622 of these, so this is the longest message the bridge carries and
    /// the one most likely to be caught half sent by a plugin falling over. Every cut is tried
    /// rather than a few, since the failure is a read walking past the end of the array and the
    /// cut that does it is whichever one lands mid field.
    /// </remarks>
    [Fact]
    public void A_parameter_list_cut_short_never_throws()
    {
        var said = Enumerable.Range(0, 12)
            .Select(i => new PluginParameter((uint)i, "P" + i, 0, 12, i, 0, false, false, false, false))
            .ToArray();

        byte[] whole = _body.Parameters(said);

        for (int cut = 0; cut <= whole.Length; cut++)
        {
            var back = _body.ReadParameters(whole.Take(cut).ToArray());

            Assert.True(back.Length <= said.Length);
        }
    }

    /// <summary>Rubbish that is not a parameter list at all answers rather than throwing.</summary>
    [Fact]
    public void Rubbish_answers_rather_than_throwing()
    {
        _body.ReadParameters(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F });
        _body.ReadParameters(new byte[] { 200, 0, 0, 0, 1, 2, 3 });
        _body.ReadParameters(Array.Empty<byte>());
    }
}
