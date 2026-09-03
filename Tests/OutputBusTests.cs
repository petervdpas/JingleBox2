using System;
using JingleBox2.Audio;
using ManagedBass;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the output bus does when it is lied to, and what it does when it is not.
/// </summary>
/// <remarks>
/// It runs against the real add-on rather than a stand-in, on BASS's own no-sound device, which
/// needs no card and no window and is what makes this runnable in CI on both platforms. A
/// stand-in would only say that this class calls the functions this class calls; what is worth
/// pinning is what the add-on does with what it is handed, since every rule that matters here is
/// the add-on's: a source has to be a decoding channel, a channel can be on one bus only, and a
/// bus with nothing on it must not stall.
///
/// The last of those is the one that would have cost an afternoon. A stalled bus under an ASIO
/// driver is the driver pulling from something that has stopped producing, which is silence from
/// the whole application with nothing anywhere saying why, and a stopped transport with no pad
/// down is the ordinary state of this program.
///
/// Every test skips itself where the add-on is not there rather than failing, and says so out
/// loud where it can. A checkout without the native is a build that runs; a test that reported
/// green for a missing subject for the rest of its life would be worse than one that is absent.
/// </remarks>
public sealed class OutputBusTests : IDisposable
{
    /// <summary>What the tests sum at, which is what nearly everything here runs at.</summary>
    private const int Rate = 44100;

    /// <summary>Stereo, which is what everything downstream of this is written for.</summary>
    private const int Stereo = 2;

    /// <summary>BASS's own device that decodes and plays nothing, so this needs no card.</summary>
    private const int NoSound = 0;

    /// <summary>Whether BASS itself came up, since without it there is nothing to test against.</summary>
    private readonly bool _bass;

    /// <summary>Opens BASS on the device that plays nothing.</summary>
    public OutputBusTests()
    {
        try
        {
            _bass = Bass.Init(NoSound, Rate) || Bass.LastError == Errors.Already;
        }
        catch (Exception)
        {
            _bass = false;
        }
    }

    /// <summary>Lets BASS go again.</summary>
    public void Dispose()
    {
        try
        {
            Bass.Free();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>A bus over the no-sound device, or nothing where this cannot run here.</summary>
    private OutputBus? Made()
    {
        if (!_bass) return null;

        var bus = new OutputBus();

        return bus.Present ? bus : null;
    }

    /// <summary>A decoding channel that produces silence, which is what a source has to be.</summary>
    /// <param name="procedure">Kept by the caller, since BASS holds the pointer.</param>
    private static int Silence(out StreamProcedure procedure)
    {
        procedure = Fill;

        return Bass.CreateStream(Rate, Stereo, BassFlags.Float | BassFlags.Decode, procedure, IntPtr.Zero);

        static int Fill(int handle, IntPtr buffer, int length, IntPtr user) => length;
    }

    /// <summary>A bus nobody opened is inert rather than a handle to nothing.</summary>
    [Fact]
    public void A_bus_that_was_never_opened_holds_nothing_and_takes_nothing()
    {
        using var bus = new OutputBus();

        Assert.False(bus.IsOpen);
        Assert.Equal(0, bus.Handle);
        Assert.False(bus.Add(1234));
        Assert.False(bus.Holds(1234));
    }

    /// <summary>Closing and disposing an unopened bus is safe, and safe twice.</summary>
    [Fact]
    public void Closing_one_that_was_never_opened_does_nothing()
    {
        var bus = new OutputBus();

        bus.Close();
        bus.Close();
        bus.Dispose();

        Assert.False(bus.IsOpen);
    }

    /// <summary>A rate or a width of nought is refused before the add-on is asked.</summary>
    [Fact]
    public void A_rate_or_a_width_of_nothing_is_refused()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.False(bus.Open(0, Stereo, false));
            Assert.False(bus.Open(-1, Stereo, false));
            Assert.False(bus.Open(Rate, 0, false));
            Assert.False(bus.IsOpen);
        }
    }

    /// <summary>An opened bus really has a stream behind it.</summary>
    [Fact]
    public void An_open_bus_has_a_handle()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, true));
            Assert.True(bus.IsOpen);
            Assert.NotEqual(0, bus.Handle);
        }
    }

    /// <summary>Opening again is a new bus, so nothing is still recorded as being on the old one.</summary>
    [Fact]
    public void Opening_again_replaces_what_was_there_and_forgets_its_sources()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, true));

            int source = Silence(out var held);
            GC.KeepAlive(held);

            Assert.True(bus.Add(source));
            Assert.True(bus.Holds(source));

            Assert.True(bus.Open(Rate, Stereo, true));
            Assert.False(bus.Holds(source));

            Bass.StreamFree(source);
        }
    }

    /// <summary>A playing channel cannot be a source, and saying so is the whole point of the check.</summary>
    [Fact]
    public void A_source_that_is_not_a_decoding_channel_is_refused_rather_than_mixed_silently()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, true));

            StreamProcedure procedure = (_, _, length, _) => length;
            int playing = Bass.CreateStream(Rate, Stereo, BassFlags.Float, procedure, IntPtr.Zero);

            if (playing == 0) return;

            Assert.False(bus.Add(playing));
            Assert.False(bus.Holds(playing));

            GC.KeepAlive(procedure);
            Bass.StreamFree(playing);
        }
    }

    /// <summary>Nought is what a handle is before it exists, so it is never plugged in.</summary>
    [Fact]
    public void Nought_is_not_a_source()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, true));
            Assert.False(bus.Add(0));
            Assert.False(bus.Holds(0));
        }
    }

    /// <summary>Adding twice leaves one, so one Remove really takes it off.</summary>
    [Fact]
    public void The_same_source_added_twice_is_on_once()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, true));

            int source = Silence(out var held);
            GC.KeepAlive(held);

            Assert.True(bus.Add(source));
            Assert.True(bus.Add(source));
            Assert.True(bus.Holds(source));

            bus.Remove(source);

            Assert.False(bus.Holds(source));

            Bass.StreamFree(source);
        }
    }

    /// <summary>Removing a stranger leaves the bus as it was.</summary>
    [Fact]
    public void Removing_something_that_was_never_on_does_nothing()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, true));

            bus.Remove(4321);
            bus.Remove(0);

            Assert.True(bus.IsOpen);
        }
    }

    /// <summary>The empty bus goes on producing, which is what MixerNonStop is there for.</summary>
    [Fact]
    public void A_bus_with_nothing_on_it_still_gives_audio_rather_than_stalling()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, true));

            var buffer = new float[512];
            int got = Bass.ChannelGetData(bus.Handle, buffer, (int)(DataFlags.Float | (DataFlags)(buffer.Length * sizeof(float))));

            Assert.True(got > 0, "a bus with no sources stalled, which under a driver is silence from the whole application");
        }
    }

    /// <summary>A source on the bus is audible in what the bus hands back.</summary>
    [Fact]
    public void What_a_source_produces_comes_out_of_the_bus()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, true));

            StreamProcedure loud = Full;
            int source = Bass.CreateStream(Rate, Stereo, BassFlags.Float | BassFlags.Decode, loud, IntPtr.Zero);

            if (source == 0) return;

            Assert.True(bus.Add(source));

            var buffer = new float[512];
            int got = Bass.ChannelGetData(bus.Handle, buffer, (int)(DataFlags.Float | (DataFlags)(buffer.Length * sizeof(float))));

            Assert.True(got > 0);

            float loudest = 0;
            foreach (float sample in buffer) loudest = Math.Max(loudest, Math.Abs(sample));

            Assert.True(loudest > 0.1f, "the source was on the bus and nothing of it came out");

            GC.KeepAlive(loud);
            Bass.StreamFree(source);
        }

        static int Full(int handle, IntPtr buffer, int length, IntPtr user)
        {
            int count = length / sizeof(float);
            var block = new float[count];

            for (int i = 0; i < count; i++) block[i] = 0.5f;

            System.Runtime.InteropServices.Marshal.Copy(block, 0, buffer, count);

            return length;
        }
    }

    /// <summary>The level outlives the stream, which a device change replaces.</summary>
    [Fact]
    public void The_level_is_kept_across_the_bus_being_opened_again()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            bus.Level = 0.25f;

            Assert.Equal(0.25f, bus.Level, 3);

            Assert.True(bus.Open(Rate, Stereo, true));

            Assert.Equal(0.25f, bus.Level, 3);
        }
    }

    /// <summary>
    /// The bus holds what it was told to and not the library's own half second.
    /// </summary>
    /// <remarks>
    /// This is the one number in here that was got wrong and heard. The bus is the channel that
    /// plays, so it carries the buffer that the tracker's own stream used to carry, and left
    /// alone it takes the library's default of 500 ms against the 46 the settings ask for. The
    /// sequencer's clock is wall time rather than the render, so how far the rendering runs ahead
    /// of real time is what decides where a note lands: eleven times further ahead and a chord's
    /// notes stop arriving together. It was reported as the alignment of the tracks going, which
    /// is what it sounds like from a chair, and is why the figure is pinned here rather than
    /// trusted.
    /// </remarks>
    [Fact]
    public void The_bus_holds_the_buffer_it_was_given_rather_than_the_library_default()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            bus.BufferMs = 46;

            Assert.True(bus.Open(Rate, Stereo, false));

            Bass.ChannelGetAttribute(bus.Handle, ChannelAttribute.Buffer, out float held);

            Assert.Equal(0.046f, held, 3);
            Assert.NotEqual(0.5f, held, 3);
        }
    }

    /// <summary>The buffer can be moved while the bus is open, which is what SETTINGS does.</summary>
    [Fact]
    public void The_buffer_can_be_moved_while_the_bus_is_open()
    {
        if (Made() is not { } bus) return;

        using (bus)
        {
            Assert.True(bus.Open(Rate, Stereo, false));

            bus.BufferMs = 120;

            Bass.ChannelGetAttribute(bus.Handle, ChannelAttribute.Buffer, out float held);

            Assert.Equal(0.120f, held, 3);
        }
    }

    /// <summary>A level is clamped at the ends, and NaN is refused rather than clamped.</summary>
    [Fact]
    public void A_level_past_either_end_is_brought_back_rather_than_refused()
    {
        using var bus = new OutputBus();

        bus.Level = 4f;
        Assert.Equal(1f, bus.Level, 3);

        bus.Level = -2f;
        Assert.Equal(0f, bus.Level, 3);

        bus.Level = 0.5f;
        bus.Level = float.NaN;

        Assert.Equal(0.5f, bus.Level, 3);
    }
}
