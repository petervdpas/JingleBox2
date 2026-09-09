namespace JingleBox2.Audio.Plugins.Bridge.Enums;

/// <summary>What one message is about. Both processes read from this same list.</summary>
public enum BridgeCall : byte
{
    /// <summary>Not a message. Nothing sends this; a zero here is a header nobody filled in.</summary>
    None = 0,

    /// <summary>Child to parent, once, when the plugin is loaded and the answer is known.</summary>
    Hello = 1,

    /// <summary>Everything the plugin exposes, sent once at the start.</summary>
    Parameters = 2,

    /// <summary>Parent to child: move a parameter.</summary>
    SetValue = 3,

    /// <summary>Parent to child: what is this set to now.</summary>
    ValueOf = 4,

    /// <summary>Parent to child: how does the plugin word this value.</summary>
    TextFor = 5,

    /// <summary>Parent to child: hand over anything queued now rather than on the next block.</summary>
    Flush = 6,

    /// <summary>Parent to child: everything inside the plugin, as a lump.</summary>
    SaveState = 7,

    /// <summary>Parent to child: put a lump back.</summary>
    LoadState = 8,

    /// <summary>Parent to child: open the plugin's own interface.</summary>
    OpenEditor = 9,

    /// <summary>Parent to child: put the interface inside this window.</summary>
    Attach = 10,

    /// <summary>Parent to child: take it back out.</summary>
    Detach = 11,

    /// <summary>Parent to child: the window is now this size.</summary>
    Resized = 12,

    /// <summary>Parent to child: put the interface away.</summary>
    CloseEditor = 13,

    /// <summary>Child to parent, unasked: the plugin wants a different size.</summary>
    ResizeRequested = 14,

    /// <summary>Parent to child: stop.</summary>
    Quit = 15,

    /// <summary>The answer to anything that only needs a yes.</summary>
    Ok = 16,

    /// <summary>The answer to anything that went wrong, with a reason.</summary>
    Fail = 17,

    /// <summary>The answer to a question whose answer is a number.</summary>
    Value = 18,

    /// <summary>The answer to a question whose answer is words.</summary>
    Text = 19,

    /// <summary>The answer to a question whose answer is a lump of bytes.</summary>
    State = 20,

    /// <summary>Parent to child on the audio socket: there is a block waiting.</summary>
    Process = 21,

    /// <summary>Child to parent on the audio socket: the block is done.</summary>
    Rendered = 22,

    /// <summary>Child to parent, unasked: something worth putting in the log.</summary>
    Note = 23,

    /// <summary>Child to parent, unasked: the plugin moved one of its own knobs.</summary>
    Edited = 24,

    /// <summary>Child to parent, unasked: everything about the plugin may have changed at once.</summary>
    Reloaded = 25,

    /// <summary>
    /// Parent to child: what every parameter stands at. Child to parent: the answer, which is
    /// every id and its value.
    /// </summary>
    /// <remarks>
    /// One call for the lot rather than <see cref="ValueOf"/> per parameter, and it is the same
    /// member both ways for the same reason <see cref="Parameters"/> is: the question and the
    /// answer are about one thing and a second name for the answer would be a second thing to
    /// keep in step.
    ///
    /// It exists because writing a chain down asked one parameter at a time. A plugin with five
    /// thousand of them cost five thousand round trips to save a song or take one undo step,
    /// which is about a second of the caller standing still and was measured pushing a block of
    /// audio past its own budget.
    /// </remarks>
    Values = 26,

    /// <summary>Parent to child: move every one of these parameters, in one message.</summary>
    /// <remarks>
    /// <see cref="Values"/>'s other half, and it exists for the same measurement read the other
    /// way round. Putting a chain back wrote one parameter at a time, and every one of those is a
    /// round trip because the child answers everything: a plugin with five thousand parameters
    /// cost five thousand of them to open a song or to add the plugin to a track.
    ///
    /// A separate member rather than <see cref="Values"/> with a payload on it. One name meaning
    /// ask when it is empty and set when it is not is the kind of cleverness that is read wrongly
    /// once and then carried for years.
    /// </remarks>
    SetValues = 27
}
