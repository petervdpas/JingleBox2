using System;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Config;

/// <inheritdoc/>
/// <remarks>
/// **The value is in the settings and the bus is told**, which is the whole of it. The level is
/// kept in decibels, since that is what a fader reads and what somebody would recognise in a
/// settings file, and the bus is handed the amplitude it multiplies by.
///
/// Where a strip's level is not a bus's, which is the recording input's, the two closures are
/// what it is instead: that fader is a gain on what is coming in and the number lives in
/// <see cref="AppConfig.RecordGainDb"/>.
/// </remarks>
public sealed class DeskStrip : IDeskStrip
{
    /// <summary>What the settings file holds for this strip.</summary>
    private readonly DeskStripConfig _kept;

    /// <summary>The bus underneath, or nothing for a strip whose level is not a bus's.</summary>
    private readonly Audio.Interfaces.IOutputBus? _bus;

    /// <summary>How a reading in decibels becomes what the engine multiplies by.</summary>
    private readonly UI.Interfaces.IGainScale _gain;

    /// <summary>Where the level is read from, where that is not this strip's own bus.</summary>
    private readonly Func<double>? _reads;

    /// <summary>And where it is written to.</summary>
    private readonly Action<double>? _writes;

    /// <summary>Said whenever anything here moves, so the settings reach the disc.</summary>
    private readonly Action _moved;

    /// <summary>
    /// A strip over a bus, whose level is the bus's own.
    /// </summary>
    /// <param name="kept">What the settings file holds for it.</param>
    /// <param name="bus">The bus it sets.</param>
    /// <param name="gain">How a reading becomes an amplitude.</param>
    /// <param name="moved">Said whenever anything here moves.</param>
    public DeskStrip(
        DeskStripConfig kept,
        Audio.Interfaces.IOutputBus bus,
        UI.Interfaces.IGainScale gain,
        Action moved)
    {
        _kept = kept;
        _bus = bus;
        _gain = gain;
        _moved = moved;
    }

    /// <summary>
    /// A strip whose level is somebody else's, which is the recording input's gain.
    /// </summary>
    /// <param name="kept">What the settings file holds for it.</param>
    /// <param name="bus">The bus its pan and mute set, which for the input is the monitor.</param>
    /// <param name="gain">How a reading becomes an amplitude.</param>
    /// <param name="reads">Where the level is read from.</param>
    /// <param name="writes">And where it is written to.</param>
    /// <param name="moved">Said whenever anything here moves.</param>
    public DeskStrip(
        DeskStripConfig kept,
        Audio.Interfaces.IOutputBus? bus,
        UI.Interfaces.IGainScale gain,
        Func<double> reads,
        Action<double> writes,
        Action moved)
    {
        _kept = kept;
        _bus = bus;
        _gain = gain;
        _reads = reads;
        _writes = writes;
        _moved = moved;
    }

    /// <summary>
    /// Puts what the settings hold onto the bus, which is what a start is.
    /// </summary>
    /// <remarks>
    /// Called once the engine is open, since a bus that is not there yet cannot be told anything.
    /// The level is left alone where it is somebody else's: whoever owns it puts it back.
    /// </remarks>
    public void Restore()
    {
        if (_bus == null) return;

        if (_reads == null) _bus.Level = (float)_gain.ToAmplitude(_kept.Level);

        _bus.Pan = _kept.Pan;
        _bus.Mute = _kept.Mute;
    }

    /// <inheritdoc/>
    public double Level
    {
        get => _reads != null ? _reads() : _kept.Level;
        set
        {
            if (Math.Abs(Level - value) < 0.0001) return;

            if (_writes != null)
            {
                _writes(value);

                return;
            }

            _kept.Level = value;

            if (_bus != null) _bus.Level = (float)_gain.ToAmplitude(value);

            _moved();
        }
    }

    /// <inheritdoc/>
    public double Pan
    {
        get => _kept.Pan;
        set
        {
            if (Math.Abs(_kept.Pan - value) < 0.0001) return;

            _kept.Pan = value;

            if (_bus != null) _bus.Pan = value;

            _moved();
        }
    }

    /// <inheritdoc/>
    public bool Mute
    {
        get => _kept.Mute;
        set
        {
            if (_kept.Mute == value) return;

            _kept.Mute = value;

            if (_bus != null) _bus.Mute = value;

            _moved();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing is told here: what a solo comes to is worked out over the whole row and said to
    /// the output bus at once, since it means only this.
    /// </remarks>
    public bool Solo
    {
        get => _kept.Solo;
        set
        {
            if (_kept.Solo == value) return;

            _kept.Solo = value;

            _moved();
        }
    }
}
