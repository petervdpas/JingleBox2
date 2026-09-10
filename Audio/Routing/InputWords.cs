using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing;

/// <inheritdoc/>
public sealed class InputWords : IInputWords
{
    /// <inheritdoc/>
    public string Taking(AudioRoute source) =>
        source is null ? "" : "Taking audio from " + source.Name + "...";

    /// <inheritdoc/>
    public string Line(AudioRoute? source, bool heard, bool canHear, InputAside aside, bool connected)
    {
        if (source is null) return "";

        if (!connected)
            return source.Name + " is not giving anything to record yet. It will be picked up as "
                + "soon as it does.";

        if (!canHear)
            return source.Display + " is what this application plays out of, so what it is playing "
                + "cannot also be heard through it: that is a loop. Anything else the recorder is "
                + "carrying still is.";

        if (aside == InputAside.Refused)
            return source.Display + " is being recorded, but this machine could not take it off "
                + "its own output, so it is still playing where it was.";

        if (aside == InputAside.Moved)
            return heard
                ? source.Display + " is coming through the desk and nowhere else."
                : source.Display + " is on the input, off its own output, and is not being heard.";

        return heard
            ? "Recording from " + source.Display + ", and hearing it through the desk."
            : "Recording from " + source.Display + ".";
    }
}
