namespace JingleBox2.Midi;

/// <summary>
/// One thing a hardware control can drive.
/// </summary>
/// <remarks>
/// Deliberately the smallest possible shape: what it is called, what range it lives in, where
/// it is, and how to move it. Everything the program has turned out to fit it without being
/// changed, which is the whole reason mapping hardware is a small job here rather than a large
/// one. A machine parameter carries its own <c>Min</c> and <c>Max</c>, a plugin parameter
/// carries the same two under the same names, and a mixer strip is a handful of known ranges.
///
/// So there is no per machine work, and a machine somebody writes next year is mappable the day
/// it lands, because it already says what its parameters are.
/// </remarks>
public interface IControlTarget
{
    /// <summary>What to call it, for a list of mappings and for the status line.</summary>
    string Name { get; }

    double Min { get; }

    double Max { get; }

    /// <summary>Where it is now, which is what pickup compares the knob against.</summary>
    double Value { get; }

    /// <summary>Moves it. Clamping is the target's own business.</summary>
    void Set(double value);
}

/// <summary>
/// Where a mapping is turned into the thing it names, as things stand this second.
/// </summary>
/// <remarks>
/// Asked on every message rather than resolved once and held, because what a mapping names
/// moves underneath it: a track's instrument is swapped, a plugin is taken out of a chain, a
/// song is closed. Holding a target across any of those is holding something that has gone.
/// Answering null is ordinary and means the knob does nothing, which is the right thing for a
/// mapping that is about a track this song has not got.
/// </remarks>
public interface IControlTargets
{
    IControlTarget? Find(ControlMapping mapping);
}
