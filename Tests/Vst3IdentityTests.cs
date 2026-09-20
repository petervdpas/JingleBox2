using JingleBox2.Audio.Plugins;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The interface ids this host asks a VST3 plugin for, against the ones Steinberg published.
/// </summary>
/// <remarks>
/// **An id that is wrong fails in silence**, which is the whole reason these are written down
/// twice. A <c>QueryInterface</c> for an id nobody answers is not an error: it is a plugin saying
/// it does not do that thing, which is an ordinary answer and is what a host must accept. So one
/// wrong digit is indistinguishable from every plugin in the world declining, for ever.
///
/// It cost exactly that. <see cref="Vst3Abi.MidiMappingId"/> had three of its four words wrong,
/// so the modulation and pitch wheels reached no VST3 plugin at all and nothing anywhere said so;
/// what named it was a line in the log written at the moment the query came back empty.
///
/// The expected bytes are the SDK's own, from <c>pluginterfaces/vst/ivsteditcontroller.h</c> and
/// its neighbours, spelled here in the order the SDK spells them. That is not the same fact said
/// twice: one side is what this application asks for and the other is what the standard says, and
/// the only way they can agree is if somebody copied them right.
///
/// Both byte orders, because VST3 lays an id out one way on Windows and another everywhere else,
/// and a host that gets that wrong works on one machine and answers nothing on the other. The
/// tests run on both.
/// </remarks>
public class Vst3IdentityTests
{
    /// <summary>The id the wheels go through, which is the one that was wrong.</summary>
    [Fact]
    public void The_midi_mapping_id_is_the_published_one()
    {
        Same(Vst3Abi.MidiMappingId, 0xDF0FF9F7, 0x49B74669, 0xB63AB732, 0x7ADBF5E5);
    }

    /// <summary>And the two halves a plugin is made of, which everything else is asked of.</summary>
    [Fact]
    public void The_two_halves_have_the_published_ids()
    {
        Same(Vst3Abi.AudioProcessorId, 0x42043F99, 0xB7DA453C, 0xA569E79D, 0x9AAEC33D);
        Same(Vst3Abi.EditControllerId, 0xDCD7BBE3, 0x7742448D, 0xA874AACC, 0x979C759E);
    }

    /// <summary>
    /// The controller numbers the wheels are asked about are MIDI's own.
    /// </summary>
    /// <remarks>
    /// One is the modulation wheel in the MIDI specification, and pitch bend has a status byte
    /// rather than a controller number, so VST3 numbers it past the end of the 128 at 129. A
    /// wrong number here fails exactly as a wrong id does, which is silently.
    /// </remarks>
    [Fact]
    public void The_wheels_are_numbered_as_midi_numbers_them()
    {
        Assert.Equal(1, Vst3Abi.ModulationController);
        Assert.Equal(129, Vst3Abi.PitchBendController);
    }

    /// <summary>
    /// No two of them are the same, which is what a typo most often produces.
    /// </summary>
    [Fact]
    public void No_two_ids_are_the_same()
    {
        var ids = new[]
        {
            Vst3Abi.AudioProcessorId, Vst3Abi.EditControllerId, Vst3Abi.MidiMappingId,
            Vst3Abi.ConnectionPointId
        };

        for (int one = 0; one < ids.Length; one++)
            for (int other = one + 1; other < ids.Length; other++)
                Assert.False(System.Linq.Enumerable.SequenceEqual(ids[one], ids[other]),
                             "two interface ids are the same bytes");
    }

    /// <summary>
    /// Compares an id with the four words the SDK declares it as.
    /// </summary>
    /// <remarks>
    /// Laid out here the way this machine lays one out rather than the way the other one does,
    /// since what is being checked is the words and not the ordering: <see cref="Vst3Abi.Uid"/>
    /// is the one place that knows which order this platform wants, and a second spelling of that
    /// rule here would be the same fact written twice and free to drift.
    /// </remarks>
    /// <param name="id">What the application asks for.</param>
    /// <param name="first">The four words as the SDK declares them.</param>
    /// <param name="second">See <paramref name="first"/>.</param>
    /// <param name="third">See <paramref name="first"/>.</param>
    /// <param name="fourth">See <paramref name="first"/>.</param>
    private static void Same(byte[] id, uint first, uint second, uint third, uint fourth)
    {
        Assert.Equal(Vst3Abi.Uid(first, second, third, fourth), id);
    }
}
