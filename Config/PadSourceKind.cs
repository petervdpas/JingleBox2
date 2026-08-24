namespace JingleBox2.Config;

/// <summary>
/// What a pad plays.
/// </summary>
/// <remarks>
/// A recording off the shelf or a stream from the web, and nothing else. A pad used to take
/// any file on the disc, which is a jingle waiting to go silent the next time somebody tidies
/// their downloads folder. Bring it in on the RECORD tab and the app owns it.
///
/// The numbers are what is written in config.json, so they stay where they are: a pad saved
/// as a file plays as a recording without anything being migrated.
/// </remarks>
public enum PadSourceKind
{
    None = 0,
    Recording = 1,
    Stream = 2
}
