using JingleBox2.Midi;
using System.Collections.Generic;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// One delivery from a port, which is one message or several.
/// </summary>
/// <remarks>
/// This is the fault that hung keys, and it hid behind the shape of the traffic. Pressing a
/// chord arrives as three deliveries a millisecond or two apart, because a hand does not put
/// three fingers down at exactly one instant; letting go of one arrives as a single delivery
/// with three note offs in it, because lifting a hand is one movement. Reading the first
/// message of a delivery and dropping the rest therefore let every press through and swallowed
/// two releases out of three, which reads as "the release is never sent" from every point in
/// the program above it.
/// </remarks>
public class MidiDeliveryTests
{
    /// <summary>A hand off a three-note chord: one delivery, three releases.</summary>
    [Fact]
    public void Every_message_in_a_delivery_is_read()
    {
        var said = Read(0x80, 60, 0, 0x80, 64, 0, 0x80, 67, 0);

        Assert.Equal(new[] { "up 60", "up 64", "up 67" }, said);
    }

    /// <summary>And the same chord let go of in running status, which is what most devices send.</summary>
    [Fact]
    public void Through_running_status_inside_one_delivery()
    {
        var said = Read(0x90, 60, 0, 64, 0, 67, 0);

        Assert.Equal(new[] { "up 60", "up 64", "up 67" }, said);
    }

    [Fact]
    public void A_press_and_its_release_can_share_a_delivery()
    {
        var said = Read(0x90, 60, 100, 0x80, 60, 0);

        Assert.Equal(new[] { "down 60", "up 60" }, said);
    }

    /// <summary>
    /// A realtime byte threaded between two messages is stepped over, not read as a note.
    /// </summary>
    /// <remarks>
    /// A device sending clock puts one anywhere it likes, including between the messages of a
    /// chord. It has to cost the messages around it nothing.
    /// </remarks>
    [Fact]
    public void A_clock_byte_between_two_messages_costs_neither()
    {
        var said = Read(0x80, 60, 0, 0xF8, 0x80, 64, 0);

        Assert.Equal(new[] { "up 60", "up 64" }, said);
    }

    /// <summary>
    /// Half a message at the end of a delivery is dropped, and the whole ones before it are not.
    /// </summary>
    /// <remarks>
    /// The old reader dropped everything after the first message, so this case and the good one
    /// looked identical from outside. They are not: what is complete has to arrive.
    /// </remarks>
    [Fact]
    public void A_message_cut_short_does_not_cost_the_ones_before_it()
    {
        var said = Read(0x80, 60, 0, 0x80, 64);

        Assert.Equal(new[] { "up 60" }, said);
    }

    /// <summary>
    /// A system exclusive message followed by a note, in one delivery.
    /// </summary>
    /// <remarks>
    /// The gatherer used to take the whole delivery whether or not the message ended inside it.
    /// An identity reply arriving in the same breath as a key press would have cost the key.
    /// </remarks>
    [Fact]
    public void A_system_exclusive_message_ends_where_it_ends()
    {
        var said = Read(0xF0, 0x7E, 0x00, 0x06, 0x02, 0xF7, 0x90, 60, 100);

        Assert.Equal(new[] { "sysex", "down 60" }, said);
    }

    /// <summary>
    /// A device that abandons a system exclusive message part way is not left mid-gather.
    /// </summary>
    /// <remarks>
    /// The status byte that interrupted it starts the next message and must be read as one,
    /// which means the reader has to be handed it back rather than eating it. Getting that
    /// wrong is a loop, so it is worth a test of its own.
    /// </remarks>
    [Fact]
    public void An_abandoned_system_exclusive_message_gives_the_next_one_back()
    {
        var said = Read(0xF0, 0x7E, 0x00, 0x90, 60, 100);

        Assert.Equal(new[] { "down 60" }, said);
    }

    /// <summary>Data with no status in front of it and nothing to read it against is stepped over.</summary>
    [Fact]
    public void Nonsense_at_the_head_of_a_delivery_does_not_stop_the_rest()
    {
        var said = Read(60, 100, 0x90, 62, 100);

        Assert.Equal(new[] { "down 62" }, said);
    }

    /// <summary>Reads one delivery the way the port hands it over, and says what came out.</summary>
    private static IReadOnlyList<string> Read(params int[] bytes)
    {
        var data = new byte[bytes.Length];
        for (int at = 0; at < bytes.Length; at++) data[at] = (byte)bytes[at];

        var service = new MidiService();
        var said = new List<string>();

        int from = 0;
        bool again = false;

        while (from < data.Length)
        {
            var message = service.Read("keyboard", data, from, data.Length - from, out int used);

            if (used <= 0)
            {
                if (again) break;

                again = true;
                continue;
            }

            again = false;
            from += used;

            if (message == null) continue;

            said.Add(message.Type switch
            {
                MidiMessageType.SystemExclusive => "sysex",
                MidiMessageType.Note => (message.IsOn ? "down " : "up ") + message.Value,
                _ => message.Type.ToString().ToLowerInvariant()
            });
        }

        return said;
    }
}
