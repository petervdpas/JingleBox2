using System;
using System.Collections.Generic;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the recorder's meters read: the bus a take is written from, and not the capture alone.
/// </summary>
/// <remarks>
/// A song patched into RECORD was written into the take while both meters sat dark, because both
/// read the capture's own bytes and the capture was a line input with nothing plugged into it.
/// </remarks>
public sealed class RecorderArrivingTests
{
    /// <summary>An open bus is read, whatever its fader says.</summary>
    [Fact]
    public void An_open_bus_is_what_arrives()
    {
        var service = new RecordingService();
        service.TakeFrom(new Bus { IsOpen = true, Reading = (0.4f, 0.7f), Level = 0f });

        Assert.Equal((0.4f, 0.7f), service.Arriving);
    }

    /// <summary>A bus that is not open answers nothing, so the caller reads the capture.</summary>
    [Fact]
    public void A_closed_bus_answers_nothing()
    {
        var service = new RecordingService();
        service.TakeFrom(new Bus { IsOpen = false, Reading = (0.4f, 0.7f) });

        Assert.Null(service.Arriving);
    }

    /// <summary>Before any bus has been said there is nothing to read.</summary>
    [Fact]
    public void No_bus_answers_nothing() => Assert.Null(new RecordingService().Arriving);

    /// <summary>The recorder's bus carries the input gain as its trim, for everything arriving.</summary>
    [Fact]
    public void The_bus_carries_the_gain()
    {
        var bus = new Bus { IsOpen = true };
        var service = new RecordingService { GainDb = 12 };

        service.TakeFrom(bus);

        Assert.Equal(Math.Pow(10, 12 / 20.0), bus.Gain, 4);
    }

    /// <summary>Moving the gain moves the bus's trim with it.</summary>
    [Fact]
    public void The_trim_follows_the_gain()
    {
        var bus = new Bus { IsOpen = true };
        var service = new RecordingService();
        service.TakeFrom(bus);

        service.GainDb = -6;

        Assert.Equal(Math.Pow(10, -6 / 20.0), bus.Gain, 4);
    }

    /// <summary>At 0 dB the bus trims nothing.</summary>
    [Fact]
    public void Unity_trims_nothing()
    {
        var bus = new Bus { IsOpen = true };
        var service = new RecordingService { GainDb = 12 };
        service.TakeFrom(bus);

        service.GainDb = 0;

        Assert.Equal(1f, bus.Gain);
    }

    /// <summary>A bus that says what it is told to.</summary>
    private sealed class Bus : IOutputBus
    {
        /// <inheritdoc/>
        public bool Present => true;
        /// <inheritdoc/>
        public double Pan { get; set; }
        /// <inheritdoc/>
        public bool Mute { get; set; }
        /// <inheritdoc/>
        public int Handle => 0;
        /// <inheritdoc/>
        public int BufferMs { get; set; }
        /// <inheritdoc/>
        public (float Left, float Right) Reading { get; set; }
        /// <inheritdoc/>
        public bool IsOpen { get; set; }
        /// <inheritdoc/>
        public float Level { get; set; } = 1f;
        /// <inheritdoc/>
        public float Gain { get; set; } = 1f;
        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool pulled) => false;
        /// <inheritdoc/>
        public bool Add(int source) => false;
        /// <inheritdoc/>
        public void Remove(int source) { }
        /// <inheritdoc/>
        public void HearOnly(IReadOnlyCollection<int> sources) { }
        /// <inheritdoc/>
        public int Sources => 0;
        /// <inheritdoc/>
        public bool Holds(int source) => false;
        /// <inheritdoc/>
        public void Close() { }
        /// <inheritdoc/>
        public void Dispose() { }
    }
}
