namespace JingleBox2.Rack.Controls.Interfaces;

/// <summary>
/// Whether a strip's clip light is lit, given what it has been shown.
/// </summary>
/// <remarks>
/// A clip is an instant and a light nobody sees is a light that is not there. One sample over
/// full scale can be a single hit in a whole take, so the light has to be latched and held long
/// enough for somebody looking at the desk a moment later to catch it. Every desk ever built
/// does this and holds it for a second or two.
///
/// Held rather than sticky for ever, because a light that never goes out stops meaning anything:
/// after it, every session looks like it clipped. It can also be put out by hand, which is what
/// somebody does when they have seen it and want to know whether the next take does it again.
///
/// A rule with the moment handed in rather than read, so what the light does over time can be
/// put a question to without waiting: the awkward cases here are all about when, and a test that
/// had to sleep for two seconds to ask one is a test nobody runs.
/// </remarks>
public interface IClipHold
{
    /// <summary>What a level at or over this counts as clipping.</summary>
    double Over { get; }

    /// <summary>How long the light stays lit once something has lit it, in seconds.</summary>
    double HoldSeconds { get; }

    /// <summary>
    /// Whether the light is lit, having been shown this level at this moment.
    /// </summary>
    /// <param name="level">The loudest either side reached in the reading just taken.</param>
    /// <param name="now">The moment, in seconds from anywhere, as long as it goes forward.</param>
    bool Saw(double level, double now);

    /// <summary>Puts it out, for somebody who has seen it.</summary>
    void Clear();
}
