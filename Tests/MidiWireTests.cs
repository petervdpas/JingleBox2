using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What comes off the wire, and what it is read as.
/// </summary>
/// <remarks>
/// <see cref="MidiService.Read"/> is public for exactly this: it holds the running status per
/// device, which is the one rule here subtle enough to be worth checking away from hardware, and
/// getting it wrong meant every message after the first arriving two bytes long and vanishing
/// without a word.
/// </remarks>
public class MidiWireTests
{
    private static MidiMessage? Read(MidiService service, string device, params byte[] bytes) =>
        service.Read(device, bytes, 0, bytes.Length);

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

    [Fact]
    public void A_note_on_at_no_velocity_is_a_note_off()
    {
        var message = Read(new MidiService(), "d", 0x90, 60, 0);

        Assert.False(message!.IsOn);
    }

    [Fact]
    public void A_controller_carries_its_number_and_value()
    {
        var message = Read(new MidiService(), "d", 0xB2, 74, 33);

        Assert.Equal(MidiMessageType.ControlChange, message!.Type);
        Assert.Equal(3, message.Channel);
        Assert.Equal(74, message.Value);
        Assert.Equal(33, message.Data);
    }

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

    [Fact]
    public void Running_status_is_kept_per_device()
    {
        var service = new MidiService();

        Read(service, "one", 0xB0, 74, 10);

        // The other device has said nothing, so data with no status in front of it means
        // nothing on it.
        Assert.Null(Read(service, "two", 74, 11));
    }

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

    [Fact]
    public void A_bend_says_which_channel_it_is_on_and_names_no_controller()
    {
        var message = Read(new MidiService(), "d", 0xE8, 0x00, 0x40);

        Assert.Equal(9, message!.Channel);
        Assert.Equal(0, message.Value);
        Assert.False(message.IsOn);
    }

    [Fact]
    public void A_bend_carries_through_running_status_too()
    {
        var service = new MidiService();

        Read(service, "d", 0xE0, 0x00, 0x40);

        Assert.Equal(16383, Read(service, "d", 0x7F, 0x7F)!.Data);
    }

    [Fact]
    public void A_status_that_takes_one_data_byte_does_not_eat_the_next_message()
    {
        var service = new MidiService();

        // A program change is not something this reads, and reading it as two bytes would take
        // the next message's status byte for a value and lose that message as well.
        Assert.Null(Read(service, "d", 0xC0, 5));

        Assert.Equal(60, Read(service, "d", 0x90, 60, 100)!.Value);
    }

    [Fact]
    public void System_exclusive_forgets_the_running_status()
    {
        var service = new MidiService();

        Read(service, "d", 0xB0, 74, 10);

        // Read now rather than dropped, which is what makes machine control and the identity
        // reply possible at all. What has not changed is that it ends the run.
        Assert.NotNull(Read(service, "d", 0xF0, 0x7E, 0x7F, 0x06, 0x01, 0xF7));

        // The run is over, so data on its own means nothing again.
        Assert.Null(Read(service, "d", 74, 11));
    }

    [Fact]
    public void A_real_time_byte_is_ignored_without_ending_the_run()
    {
        Assert.Null(Read(new MidiService(), "d", 0xF8));
    }

    [Fact]
    public void Nothing_at_all_reads_as_nothing()
    {
        var service = new MidiService();

        Assert.Null(service.Read("d", System.Array.Empty<byte>(), 0, 0));
        Assert.Null(service.Read("d", null!, 0, 3));
    }

    [Theory]
    [InlineData("Minilab3 MIDI", "Minilab3 MIDI   ", true)]
    [InlineData("minilab3 midi", "Minilab3 MIDI", true)]
    [InlineData("Minilab3 MIDI", "Minilab3 MCU/HUI", false)]
    [InlineData(null, "", true)]
    public void Names_are_matched_however_the_driver_padded_them(string? left, string? right, bool same) =>
        Assert.Equal(same, MidiService.SameName(left, right));

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

        // Not a press, so every other router lets it past without a line being added to any
        // of them: all three begin by asking for one.
        Assert.False(message.IsOn);
    }

    [Theory]
    [InlineData(0xF8)]
    [InlineData(0xFE)]
    public void The_clock_and_active_sensing_are_read_as_nothing(byte status) =>
        Assert.Null(Read(new MidiService(), "d", status));

    [Fact]
    public void A_realtime_byte_does_not_disturb_the_run_it_arrives_in_the_middle_of()
    {
        var service = new MidiService();

        Read(service, "d", 0xB0, 20, 40);
        Read(service, "d", 0xF8);

        // Running status still stands: the clock says nothing about what was being sent.
        var message = Read(service, "d", 21, 41);

        Assert.NotNull(message);
        Assert.Equal(MidiMessageType.ControlChange, message!.Type);
        Assert.Equal(21, message.Value);
    }

    [Fact]
    public void A_system_exclusive_message_comes_back_whole()
    {
        var message = Read(new MidiService(), "d", 0xF0, 0x7F, 0x7F, 0x06, 0x02, 0xF7);

        Assert.NotNull(message);
        Assert.Equal(MidiMessageType.SystemExclusive, message!.Type);
        Assert.Equal(new byte[] { 0xF0, 0x7F, 0x7F, 0x06, 0x02, 0xF7 }, message.Bytes);
        Assert.False(message.IsOn);
    }

    [Fact]
    public void And_it_comes_back_whole_when_it_arrived_in_pieces()
    {
        // The only message in MIDI with no length, and so the only one that can be handed over
        // a piece at a time.
        var service = new MidiService();

        Assert.Null(Read(service, "d", 0xF0, 0x00, 0x20, 0x6B));
        Assert.Null(Read(service, "d", 0x7F, 0x42));

        var message = Read(service, "d", 0x02, 0xF7);

        Assert.NotNull(message);
        Assert.Equal(new byte[] { 0xF0, 0x00, 0x20, 0x6B, 0x7F, 0x42, 0x02, 0xF7 }, message!.Bytes);
    }

    [Fact]
    public void A_clock_threaded_through_one_is_not_part_of_it()
    {
        // Which is what a device sending clock does while it answers an identity request, and
        // the specification allows it precisely there.
        var service = new MidiService();

        Read(service, "d", 0xF0, 0x7E, 0x7F);
        Read(service, "d", 0xF8);

        var message = Read(service, "d", 0x06, 0x02, 0xF7);

        Assert.NotNull(message);
        Assert.Equal(new byte[] { 0xF0, 0x7E, 0x7F, 0x06, 0x02, 0xF7 }, message!.Bytes);
    }

    [Fact]
    public void One_abandoned_part_way_does_not_swallow_what_follows()
    {
        var service = new MidiService();

        // A cable pulled mid-message. The note after it must still be read.
        Assert.Null(Read(service, "d", 0xF0, 0x00, 0x20));
        Assert.Null(Read(service, "d", 0x90, 60, 100));

        var message = Read(service, "d", 0x90, 62, 100);

        Assert.NotNull(message);
        Assert.Equal(MidiMessageType.Note, message!.Type);
        Assert.Equal(62, message.Value);
    }

    [Fact]
    public void Two_devices_send_two_system_exclusive_messages_at_once()
    {
        // Per device for the same reason running status is: one stream says nothing about the
        // other, and interleaving them would produce a message neither of them sent.
        var service = new MidiService();

        Read(service, "one", 0xF0, 0x01, 0x02);
        Read(service, "two", 0xF0, 0x11, 0x12);

        var first = Read(service, "one", 0x03, 0xF7);
        var second = Read(service, "two", 0x13, 0xF7);

        Assert.Equal(new byte[] { 0xF0, 0x01, 0x02, 0x03, 0xF7 }, first!.Bytes);
        Assert.Equal(new byte[] { 0xF0, 0x11, 0x12, 0x13, 0xF7 }, second!.Bytes);
    }
}
