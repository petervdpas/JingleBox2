namespace JingleBox2.Audio.Plugins;

/// <summary>
/// Whether the song in front of us was written on a machine like this one.
/// </summary>
/// <remarks>
/// Static, for the reason the other doors here are: one song is open at a time and handing the
/// answer about would be handing the same answer about under another name. Nothing in it decides
/// anything, which is the rule every door keeps: what the word in a song means is
/// <c>IMachineWord</c>, and what to do about it is <see cref="Interfaces.IPluginsHere"/>, either
/// of which can be asked without a song, a plugin or a file.
///
/// **What it is for is the one thing a song writes down that does not travel**, which is a path.
/// A song made on Linux and opened on Windows names its plugins at places that machine has never
/// had, so comparing those paths there is a question that can only answer no. It can also answer
/// yes and be wrong, on the day somebody carries a settings file between two machines: the list
/// of what was scanned would then hold the other machine's paths, and a match against one would
/// hand back a plugin that is not on this disc.
///
/// Off until a song says otherwise, which is what a song written before the word existed means
/// and is what every song already on anybody's disc will mean until it is saved again. Off is
/// exactly what happened before this: the paths are looked at.
/// </remarks>
public static class SongOrigin
{
    /// <summary>Whether the open song came from a different kind of machine.</summary>
    public static bool Travelled { get; private set; }

    /// <summary>Says where the song being opened was written, for everything after this.</summary>
    /// <param name="travelled">Whether it came from a different kind of machine.</param>
    public static void Wants(bool travelled) => Travelled = travelled;
}
