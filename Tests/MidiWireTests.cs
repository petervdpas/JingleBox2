using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tests;

/// <summary>
/// What comes off the wire, and what it is read as.
/// </summary>
/// <remarks>
/// <see cref="MidiService.Read"/> is public for exactly this: it holds the running status per
/// device, which is the one rule here subtle enough to be worth checking away from hardware, and
/// getting it wrong meant every message after the first arriving two bytes long and vanishing
/// without a word.
///
/// The tests run in the order the wire is read: the channel messages first (notes, controllers,
/// running status, pitch bend), then the statuses that take one data byte rather than two, then
/// port name matching, then the realtime bytes, and last the system exclusive messages, which
/// are the only ones with no length and so the only ones that can arrive in pieces.
/// </remarks>
public class MidiWireTests
{
    /// <summary>Hands one delivery to the reader for the named device, and gives back what it made of it.</summary>
    private static MidiMessage? Read(MidiService service, string device, params byte[] bytes) =>
        service.Read(device, bytes, 0, bytes.Length);

    /// <summary>A note on carries its channel, its note number and its velocity.</summary>
    [Fact]
    public void A_note_on_is_a_note()
    {
        var message = Read(new MidiService(), "d", 0x90, 60, 100);

        Assert.NotNull(message);
        Assert.Equal(MidiMessageType.Note, message!.Type);
        Assert.Equal(1, message.Channel);
        Assert.Equal(60, message.Value);
        Assert.Equal(100, message.Data);
        Assert.True(message.IsOn);
    }

    /// <summary>
    /// The other spelling of a release, and the one most keyboards actually send.
    /// </summary>
    /// <remarks>
    /// Read as a press it is a key that goes down and never comes up, which is what a light left
    /// lit looks like from above.
    /// </remarks>
    [Fact]
    public void A_note_on_at_no_velocity_is_a_note_off()
    {
        var message = Read(new MidiService(), "d", 0x90, 60, 0);

        Assert.False(message!.IsOn);
    }

    /// <summary>A controller says which number moved and where it moved to, on its own channel.</summary>
    [Fact]
    public void A_controller_carries_its_number_and_value()
    {
        var message = Read(new MidiService(), "d", 0xB2, 74, 33);

        Assert.Equal(MidiMessageType.ControlChange, message!.Type);
        Assert.Equal(3, message.Channel);
        Assert.Equal(74, message.Value);
        Assert.Equal(33, message.Data);
    }

    /// <summary>
    /// A device that sent a status once may send only data afterwards, and it still means the
    /// same kind of message.
    /// </summary>
    [Fact]
    public void Running_status_carries_the_last_kind_across()
    {
        var service = new MidiService();

        Read(service, "d", 0xB0, 74, 10);

        var second = Read(service, "d", 74, 11);

        Assert.NotNull(second);
        Assert.Equal(MidiMessageType.ControlChange, second!.Type);
        Assert.Equal(11, second.Data);
    }

    /// <summary>
    /// One device's run says nothing about another's.
    /// </summary>
    /// <remarks>
    /// The other device here has said nothing at all, so data with no status in front of it
    /// means nothing on it. Shared, the run would let one controller's traffic put a kind on
    /// another's bare bytes and produce a message nobody sent.
    /// </remarks>
    [Fact]
    public void Running_status_is_kept_per_device()
    {
        var service = new MidiService();

        Read(service, "one", 0xB0, 74, 10);

        Assert.Null(Read(service, "two", 74, 11));
    }

    /// <summary>A bend is two seven-bit halves, the least significant first, so centre is 8192.</summary>
    [Theory]
    [InlineData(0x00, 0x40, 8192)]
    [InlineData(0x00, 0x00, 0)]
    [InlineData(0x7F, 0x7F, 16383)]
    public void A_bend_is_fourteen_bits_least_significant_first(byte low, byte high, int wanted)
    {
        var message = Read(new MidiService(), "d", 0xE0, low, high);

        Assert.Equal(MidiMessageType.PitchBend, message!.Type);
        Assert.Equal(wanted, message.Data);
    }

    /// <summary>
    /// A bend belongs to a channel and not to a controller number, so nothing pointed at a
    /// controller can be reached by one.
    /// </summary>
    [Fact]
    public void A_bend_says_which_channel_it_is_on_and_names_no_controller()
    {
        var message = Read(new MidiService(), "d", 0xE8, 0x00, 0x40);

        Assert.Equal(9, message!.Channel);
        Assert.Equal(0, message.Value);
        Assert.False(message.IsOn);
    }

    /// <summary>Bends are sent in runs while a wheel moves, so the run has to hold for them too.</summary>
    [Fact]
    public void A_bend_carries_through_running_status_too()
    {
        var service = new MidiService();

        Read(service, "d", 0xE0, 0x00, 0x40);

        Assert.Equal(16383, Read(service, "d", 0x7F, 0x7F)!.Data);
    }

    /// <summary>
    /// A status that takes one data byte is stepped over by one byte, not two.
    /// </summary>
    /// <remarks>
    /// A program change is not something this reads, and reading it as two bytes would take the
    /// next message's status byte for a value and lose that message as well. So one message
    /// nobody wanted would cost the message behind it, which is a key press.
    /// </remarks>
    [Fact]
    public void A_status_that_takes_one_data_byte_does_not_eat_the_next_message()
    {
        var service = new MidiService();

        Assert.Null(Read(service, "d", 0xC0, 5));

        Assert.Equal(60, Read(service, "d", 0x90, 60, 100)!.Value);
    }

    /// <summary>
    /// A system exclusive message is read, and it ends whatever run was going on.
    /// </summary>
    /// <remarks>
    /// Reading it rather than dropping it is what makes machine control and the identity reply
    /// possible at all. What has not changed is that it ends the run: afterwards, data on its
    /// own means nothing again.
    /// </remarks>
    [Fact]
    public void System_exclusive_forgets_the_running_status()
    {
        var service = new MidiService();

        Read(service, "d", 0xB0, 74, 10);

        Assert.NotNull(Read(service, "d", 0xF0, 0x7E, 0x7F, 0x06, 0x01, 0xF7));

        Assert.Null(Read(service, "d", 74, 11));
    }

    /// <summary>A clock byte on its own is nothing to read, and is not an error either.</summary>
    [Fact]
    public void A_real_time_byte_is_ignored_without_ending_the_run()
    {
        Assert.Null(Read(new MidiService(), "d", 0xF8));
    }

    /// <summary>An empty delivery and a null buffer are both answered rather than thrown at.</summary>
    [Fact]
    public void Nothing_at_all_reads_as_nothing()
    {
        var service = new MidiService();

        Assert.Null(service.Read("d", System.Array.Empty<byte>(), 0, 0));
        Assert.Null(service.Read("d", null!, 0, 3));
    }

    /// <summary>
    /// Port names are compared with the padding and the casing the driver happened to use.
    /// </summary>
    /// <remarks>
    /// A device is known here by its port name, so a name that fails to match is a controller
    /// that has lost every link it had. Two ports of one device still have to stay apart, which
    /// is why the MCU port does not match the main one.
    /// </remarks>
    [Theory]
    [InlineData("Minilab3 MIDI", "Minilab3 MIDI   ", true)]
    [InlineData("minilab3 midi", "Minilab3 MIDI", true)]
    [InlineData("Minilab3 MIDI", "Minilab3 MCU/HUI", false)]
    [InlineData(null, "", true)]
    public void Names_are_matched_however_the_driver_padded_them(string? left, string? right, bool same) =>
        Assert.Equal(same, MidiService.SameName(left, right));

    /// <summary>
    /// Start, continue and stop are one byte each and address no channel.
    /// </summary>
    /// <remarks>
    /// Neither is a press, and <c>IsOn</c> is false on both. That is what keeps a transport byte
    /// out of the pads without a line being added anywhere: all three of the other routers begin
    /// by asking for a press.
    /// </remarks>
    [Theory]
    [InlineData(0xFA)]
    [InlineData(0xFB)]
    [InlineData(0xFC)]
    public void Start_continue_and_stop_are_one_byte_and_belong_to_no_channel(byte status)
    {
        var message = Read(new MidiService(), "d", status);

        Assert.NotNull(message);
        Assert.Equal(MidiMessageType.Realtime, message!.Type);
        Assert.Equal(status, message.Value);
        Assert.Equal(0, message.Channel);

        Assert.False(message.IsOn);
    }

    /// <summary>
    /// The clock and active sensing are dropped at the wire without a word.
    /// </summary>
    /// <remarks>
    /// At twenty four clocks a beat, a line logged per byte would drown the ones the log is kept
    /// for.
    /// </remarks>
    [Theory]
    [InlineData(0xF8)]
    [InlineData(0xFE)]
    public void The_clock_and_active_sensing_are_read_as_nothing(byte status) =>
        Assert.Null(Read(new MidiService(), "d", status));

    /// <summary>
    /// A realtime byte arriving mid-run leaves the run standing.
    /// </summary>
    /// <remarks>
    /// The clock says nothing about what was being sent, and a device sending clock puts one
    /// wherever it likes, including between the two data bytes of somebody's knob.
    /// </remarks>
    [Fact]
    public void A_realtime_byte_does_not_disturb_the_run_it_arrives_in_the_middle_of()
    {
        var service = new MidiService();

        Read(service, "d", 0xB0, 20, 40);
        Read(service, "d", 0xF8);

        var message = Read(service, "d", 21, 41);

        Assert.NotNull(message);
        Assert.Equal(MidiMessageType.ControlChange, message!.Type);
        Assert.Equal(21, message.Value);
    }

    /// <summary>A system exclusive message arrives with its own opening and closing bytes on it.</summary>
    [Fact]
    public void A_system_exclusive_message_comes_back_whole()
    {
        var message = Read(new MidiService(), "d", 0xF0, 0x7F, 0x7F, 0x06, 0x02, 0xF7);

        Assert.NotNull(message);
        Assert.Equal(MidiMessageType.SystemExclusive, message!.Type);
        Assert.Equal(new byte[] { 0xF0, 0x7F, 0x7F, 0x06, 0x02, 0xF7 }, message.Bytes);
        Assert.False(message.IsOn);
    }

    /// <summary>
    /// One handed over a piece at a time is gathered until its end byte arrives.
    /// </summary>
    /// <remarks>
    /// It is the only message in MIDI with no length, and so the only one that can be handed
    /// over in pieces at all. The buffer is kept per device, the same way the running status is.
    /// </remarks>
    [Fact]
    public void And_it_comes_back_whole_when_it_arrived_in_pieces()
    {
        var service = new MidiService();

        Assert.Null(Read(service, "d", 0xF0, 0x00, 0x20, 0x6B));
        Assert.Null(Read(service, "d", 0x7F, 0x42));

        var message = Read(service, "d", 0x02, 0xF7);

        Assert.NotNull(message);
        Assert.Equal(new byte[] { 0xF0, 0x00, 0x20, 0x6B, 0x7F, 0x42, 0x02, 0xF7 }, message!.Bytes);
    }

    /// <summary>
    /// A clock threaded through the middle of one is not part of it.
    /// </summary>
    /// <remarks>
    /// Which is what a device sending clock does while it answers an identity request, and the
    /// specification allows it precisely there. Swallowed into the lump, the reply would come
    /// back with a byte in it that the sender never meant as data.
    /// </remarks>
    [Fact]
    public void A_clock_threaded_through_one_is_not_part_of_it()
    {
        var service = new MidiService();

        Read(service, "d", 0xF0, 0x7E, 0x7F);
        Read(service, "d", 0xF8);

        var message = Read(service, "d", 0x06, 0x02, 0xF7);

        Assert.NotNull(message);
        Assert.Equal(new byte[] { 0xF0, 0x7E, 0x7F, 0x06, 0x02, 0xF7 }, message!.Bytes);
    }

    /// <summary>
    /// A message abandoned part way through does not swallow what comes after it.
    /// </summary>
    /// <remarks>
    /// A cable pulled mid-message is what this looks like on a desk. Any status byte other than
    /// a realtime one means the sender gave up, and the note behind it must still be read.
    /// </remarks>
    [Fact]
    public void One_abandoned_part_way_does_not_swallow_what_follows()
    {
        var service = new MidiService();

        Assert.Null(Read(service, "d", 0xF0, 0x00, 0x20));
        Assert.Null(Read(service, "d", 0x90, 60, 100));

        var message = Read(service, "d", 0x90, 62, 100);

        Assert.NotNull(message);
        Assert.Equal(MidiMessageType.Note, message!.Type);
        Assert.Equal(62, message.Value);
    }

    /// <summary>
    /// Two devices gathering at once do not get each other's bytes.
    /// </summary>
    /// <remarks>
    /// Per device for the same reason the running status is: one stream says nothing about the
    /// other, and interleaving them would produce a message neither of them sent.
    /// </remarks>
    [Fact]
    public void Two_devices_send_two_system_exclusive_messages_at_once()
    {
        var service = new MidiService();

        Read(service, "one", 0xF0, 0x01, 0x02);
        Read(service, "two", 0xF0, 0x11, 0x12);

        var first = Read(service, "one", 0x03, 0xF7);
        var second = Read(service, "two", 0x13, 0xF7);

        Assert.Equal(new byte[] { 0xF0, 0x01, 0x02, 0x03, 0xF7 }, first!.Bytes);
        Assert.Equal(new byte[] { 0xF0, 0x11, 0x12, 0x13, 0xF7 }, second!.Bytes);
    }
}
