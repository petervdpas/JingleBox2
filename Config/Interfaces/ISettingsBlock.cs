namespace JingleBox2.Config.Interfaces;

/// <summary>
/// The settings block: the document the whole application reads, and the word that it moved.
/// </summary>
/// <remarks>
/// **What a page is handed instead of a way to write the file.** The interface changes what is in
/// memory and says so; when and how that reaches the disc is not a page's business and used to be
/// spelled out at thirty four call sites, each one deciding for itself that now was the moment.
/// What that bought was a fader drag writing the file a hundred times, two threads writing one
/// file, and a field somebody added with nobody remembering to write it down.
///
/// The document is handed out rather than wrapped. It is what is serialised, every property on it
/// is a name somebody's existing file already uses, and putting a hundred and fifty of them
/// behind a second spelling would be a second document to keep in step.
/// </remarks>
public interface ISettingsBlock : IMemoryBlock
{
    /// <summary>The settings themselves, which is the same object everything else is holding.</summary>
    AppConfig Config { get; }

    /// <summary>
    /// Says that something in the settings has just been changed.
    /// </summary>
    /// <remarks>
    /// A hint, for the reason written on <see cref="IMemoryBlock.Changed"/>: it makes the file
    /// keep up rather than making it correct, so forgetting it costs a moment rather than
    /// somebody's work. There is nothing to pass, because a document written whole has no such
    /// thing as part of it having changed.
    /// </remarks>
    void Moved();
}
