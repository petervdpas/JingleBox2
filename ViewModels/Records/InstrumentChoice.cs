using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.ViewModels.Records;

/// <summary>
/// One thing a song can take an instrument from: a machine on the rack, or a plugin.
/// </summary>
/// <remarks>
/// The two together, because to a track they are one question with one answer: what plays this
/// part. A machine is this installation's own and is on the rack; a plugin is somebody else's
/// program and is on the computer, used by a song rather than owned by the rack. Neither is more
/// of an instrument than the other, so they are picked from one list.
///
/// Exactly one of the two is set. Which it is decides how the song's own copy is made, and
/// nothing else about the row differs.
/// </remarks>
/// <param name="Machine">The rack's box, or nothing when this row is a plugin.</param>
/// <param name="Plugin">The plugin, or nothing when this row is a machine.</param>
/// <param name="Said">
/// What to call it. A plugin says its format where the same name is installed twice, which
/// happens often, since a great many plugins ship as a CLAP and a VST3 of the same thing and
/// those are two different plugins here: two ids, two sets of parameter numbers, and two
/// templates that are not interchangeable.
/// </param>
/// <param name="Colour">The dot down the side, which is the machine's own or the plugin's grey.</param>
public sealed record InstrumentChoice(RackSoundMachine? Machine, PluginInfo? Plugin, string Said, string Colour);
