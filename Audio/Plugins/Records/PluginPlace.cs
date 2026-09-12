using JingleBox2.Audio.Plugins.Enums;

namespace JingleBox2.Audio.Plugins.Records;

/// <summary>
/// One folder a scan looks in, and which of the two standards it is looked in for.
/// </summary>
/// <remarks>
/// **The format is half of what a place is, and leaving it out made the list unreadable.** The
/// two standards keep their plugins in different folders, and a machine has six or seven of them
/// at once: shown as bare paths, <c>/usr/lib/clap</c> and <c>/usr/lib/vst3</c> are two lines that
/// only say which is which by the last word of the path, and a folder somebody added by hand,
/// which is looked in for both, says nothing at all about why it is there twice.
///
/// It is also what makes a place switchable at all. A folder added by hand is looked in by both
/// scanners, so turning it off has to mean one of them or the other: a folder full of CLAPs that
/// is walked for VST3 bundles on every scan is time spent finding nothing, and that is exactly
/// the case somebody wants to be rid of.
///
/// Two of these are the same place when the path and the format agree. The path is compared
/// without regard to case, which is right on Windows and merely cautious here.
/// </remarks>
/// <param name="Path">The folder itself.</param>
/// <param name="Format">Which standard it is walked for.</param>
public sealed record PluginPlace(string Path, PluginFormat Format);
