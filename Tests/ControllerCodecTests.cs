using JingleBox2.Controllers;
using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Tests;

/// <summary>
/// A controller's own Lua file, which can say that these bytes mean those bytes and nothing else.
/// </summary>
/// <remarks>
/// The property worth protecting: a codec cannot add a feature or take one away, and a device
/// nobody has written a file for is passed straight through untouched.
/// <para>
/// The one shipped example is controllers/minilab3.lua, and the hard justification for a
/// scripting language at all came off the device: the Pitch strip's page has no CC field, only a
/// channel and a range, so it cannot be made to send a controller on the device and a codec is
/// the only way that strip can ever be pointed at anything.
/// </para>
/// </remarks>
public class ControllerCodecTests
{
    /// <summary>A pitch bend off a named device, which is what a MiniLab's pitch strip sends.</summary>
    private static MidiMessage Bend(string device, int value) => new()
    {
        Device = device, Type = MidiMessageType.PitchBend, Channel = 1, Value = 0, Data = value
    };

    /// <summary>
    /// The shipped codec leaves the MiniLab's pitch strip alone, at both ends and in the middle.
    /// </summary>
    /// <remarks>
    /// **A codec that translates around a gap has to be read again when the gap closes.** This
    /// file turned that strip's bend into controller 2, and its own comment said why: pitch bend
    /// reached nothing in this application, so the strip did nothing at all. It is the pitch
    /// wheel now, with no profile, no link and nothing stored, and the conversion would deliver
    /// the strip as a controller nobody is pointed at, which is the thing it was written to
    /// prevent. The lines are left in the file commented, since pointing that strip at a knob is
    /// still a thing somebody may want.
    ///
    /// Three positions rather than one, because a conversion put back would be caught by any of
    /// them and the middle is the one that reads as working when it is not: 8192 in and 64 out
    /// are both the middle of their own range.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(8192)]
    [InlineData(16383)]
    public void The_shipped_codec_leaves_the_pitch_strip_alone(int bend)
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        var read = codecs.Read(Bend("Minilab3 MIDI", bend));

        Assert.NotNull(read);
        Assert.Equal(MidiMessageType.PitchBend, read!.Type);
        Assert.Equal(bend, read.Data);
    }

    /// <summary>
    /// A codec can still turn a bend into a controller, which is the whole reason for the
    /// language.
    /// </summary>
    /// <remarks>
    /// Written here rather than read off the shipped file, and that is the point: what the
    /// MiniLab's own file does is a decision about that device and may change again, where this
    /// is the mechanism. It is dropped into the folder before the codecs are read, so nothing
    /// here waits on the watcher.
    ///
    /// The device is a made-up one, so it cannot collide with a real file and every other test in
    /// this class goes on seeing what it saw.
    /// </remarks>
    [Fact]
    public void A_codec_can_turn_a_bend_into_a_controller()
    {
        Written("testbox", """
            controller = { name = "TestBox", matches = "TestBox*" }

            function midi(m)
              if m.type == "bend" then
                return { type = "cc", channel = m.channel, number = 2, value = bit32.rshift(m.value, 7) }
              end
            end
            """);

        using var codecs = new ControllerCodecs(new NoMidi());

        var read = codecs.Read(Bend("TestBox MIDI", 8192));

        Assert.NotNull(read);
        Assert.Equal(MidiMessageType.ControlChange, read!.Type);
        Assert.Equal(2, read.Value);
        Assert.Equal(64, read.Data);
    }

    /// <summary>Puts a codec in the folder the application reads them from.</summary>
    /// <param name="name">What to call the file, without its extension.</param>
    /// <param name="lua">What is in it.</param>
    private static void Written(string name, string lua)
    {
        var folder = new ControllerFolder();

        folder.FirstRun();

        System.IO.File.WriteAllText(System.IO.Path.Combine(folder.Installed, name + ".lua"), lua);
    }

    /// <summary>
    /// A knob on a device that has a codec still arrives as itself: a codec sits between the
    /// wire and the routing and only speaks about what it was written for.
    /// </summary>
    [Fact]
    public void Anything_the_application_already_understands_is_left_alone()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        var knob = new MidiMessage
        {
            Device = "Minilab3 MIDI", Type = MidiMessageType.ControlChange,
            Channel = 1, Value = 86, Data = 33, IsOn = true
        };

        var read = codecs.Read(knob);

        Assert.Equal(86, read!.Value);
        Assert.Equal(33, read.Data);
    }

    /// <summary>
    /// The same pitch bend off a device with no codec comes out as it went in, which is what
    /// keeps a script from being a thing anybody needs.
    /// </summary>
    [Fact]
    public void A_device_with_no_codec_is_passed_straight_through()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        var read = codecs.Read(Bend("MPD218 Port A", 8192));

        Assert.Equal(MidiMessageType.PitchBend, read!.Type);
        Assert.Equal(8192, read.Data);
    }

    /// <summary>Nothing handed in is nothing handed back, rather than an exception on the
    /// MIDI thread.</summary>
    [Fact]
    public void Nothing_at_all_is_nothing()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        Assert.Null(codecs.Read(null!));
    }
}
