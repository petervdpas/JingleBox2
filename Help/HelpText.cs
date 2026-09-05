using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Help.Interfaces;
using JingleBox2.Help.Records;

namespace JingleBox2.Help;

/// <inheritdoc/>
/// <remarks>
/// The prose is not here. It is one markdown file per topic in <c>help/</c> beside the program,
/// read through <see cref="IHelpTopics"/>, and what is left in this class is the two things that
/// are really code: the ids, declared as constants so that every id a layout can ask for appears
/// somewhere a search will find it, and the keyboard page's live half.
///
/// Read once and kept, since these are files that ship rather than files anybody edits while the
/// application is running. The keyboard page is the exception and says so.
/// </remarks>
public sealed class HelpText : IHelpText
{
    /// <summary>Which device a take is recorded from, and what a loopback is.</summary>
    public const string SettingsRecordingInput = "settings.recording-input";
    /// <summary>What each piece of hardware is allowed to do, and how one is pointed at a control.</summary>
    public const string SettingsControlSurfaces = "settings.control-surfaces";
    /// <summary>Where the sound comes out, and what an ASIO driver is.</summary>
    public const string SettingsOutput = "settings.output";
    /// <summary>What this installation has, and how a box arrives or leaves.</summary>
    public const string SettingsRegistry = "settings.registry";
    /// <summary>What the audio engine runs at, and how far ahead it mixes.</summary>
    public const string SettingsEngine = "settings.engine";
    /// <summary>What the log writes down, area by area.</summary>
    public const string SettingsLog = "settings.log";
    /// <summary>Where plugins are looked for, and what happens when one crashes.</summary>
    public const string SettingsPlugins = "settings.plugins";
    /// <summary>How many pads there are, and what the limits on that are.</summary>
    public const string SettingsPadMatrix = "settings.pad-matrix";
    /// <summary>What a song's instruments are, and where they come from.</summary>
    public const string TrackerInstruments = "tracker.song-instruments";
    /// <summary>What can be written in the effect column.</summary>
    public const string TrackerEffects = "tracker.effects";
    /// <summary>What each control on a mixer strip does.</summary>
    public const string MixerStrips = "mixer.strips";
    /// <summary>What the designer's two tabs are, and what a machine and an effect each are.</summary>
    public const string DesignerWorlds = "designer.worlds";
    /// <summary>Dropping parts on a face and saying what each one turns.</summary>
    public const string DesignerLayingOut = "designer.laying-out";
    /// <summary>How loud a preset should be, and why a level knob cannot answer that.</summary>
    public const string DesignerHeadroom = "designer.headroom";
    /// <summary>Every key the application answers.</summary>
    public const string AppShortcuts = "app.shortcuts";
    /// <summary>What a pad plays, and what happens when it is hit.</summary>
    public const string PadsLayingOut = "pads.laying-out";
    /// <summary>Making a take, trimming it, and where it goes.</summary>
    public const string RecordTakes = "record.takes";
    /// <summary>How the pattern grid is read and written.</summary>
    public const string TrackerPattern = "tracker.pattern";
    /// <summary>What a song holds and how one travels.</summary>
    public const string TrackerSong = "tracker.song";
    /// <summary>A control moving over the lines of a pattern.</summary>
    public const string TrackerAutomation = "tracker.automation";
    /// <summary>What your hardware is pointed at, and how a template travels.</summary>
    public const string MidiTemplates = "midi.templates";
    /// <summary>What this installation has on its rack, and what a box on it is.</summary>
    public const string RackDevices = "rack.devices";

    /// <summary>
    /// Where the topics are: <c>help/</c> beside the program.
    /// </summary>
    /// <remarks>
    /// Beside the program and not under the application folder, unlike a machine or a controller
    /// profile, because this is the program explaining itself rather than anything of yours:
    /// there is nothing here to keep when a new version arrives and nothing anybody would edit
    /// and want back. The sources are in <c>Help/Topics/</c> and land here under a lowercase
    /// name, the way a controller profile does and for the same reason: a folder called
    /// <c>help</c> beside the <c>Help</c> the code is in differs only in case, which is two
    /// folders here and one on Windows.
    /// </remarks>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "help");

    /// <summary>
    /// The two lines in the keyboard page that the keys themselves are put into.
    /// </summary>
    /// <remarks>
    /// A hole in the file rather than a page built in code, so that the prose around it is
    /// written and read where all the other prose is. It is the one thing in the whole folder
    /// that is not simply what it says, and it is a whole line on its own so that finding it is
    /// a comparison rather than a search through somebody's sentence.
    /// </remarks>
    public const string SystemKeysMark = "{system keys}";

    /// <inheritdoc cref="SystemKeysMark"/>
    public const string MenuKeysMark = "{menu keys}";

    /// <summary>The topics as they were read off disc, by id.</summary>
    private readonly Dictionary<string, HelpTopic> _topics;

    /// <summary>Which keys the four editable shortcuts are on, said as a list.</summary>
    private readonly IShortcutSheet _keys;

    /// <summary>Reads the topics that ship, unless somewhere else is named.</summary>
    /// <param name="keys">The editable shortcuts, or nothing for the application's own.</param>
    /// <param name="topics">How a folder is read, or nothing for the ordinary way.</param>
    /// <param name="folder">Where to read from, or nothing for the folder beside the program.</param>
    public HelpText(IShortcutSheet? keys = null, IHelpTopics? topics = null, string? folder = null)
    {
        _keys = keys ?? new ShortcutSheet();

        _topics = (topics ?? new HelpTopics())
            .In(folder ?? Folder)
            .ToDictionary(topic => topic.Id, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public HelpTopic? Find(string? id) =>
        !string.IsNullOrWhiteSpace(id) && _topics.TryGetValue(id, out var topic) ? Filled(topic) : null;

    /// <inheritdoc/>
    /// <remarks>
    /// Sorted by title rather than by id, since the title is what the list shows, and worked out
    /// when it is asked for because the keyboard page is only true at the moment it is asked
    /// for. Sorting a dozen entries is not worth keeping.
    /// </remarks>
    public IReadOnlyList<HelpTopic> All =>
        _topics.Values
            .Select(Filled)
            .OrderBy(topic => topic.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// The topic with its live half filled in, which for all but one of them is itself.
    /// </summary>
    /// <remarks>
    /// The keyboard page names four keys that are a setting, so a page kept from startup would
    /// be a page of defaults for the rest of the session: the settings are read out of the file
    /// after the first of these exists, and they are edited afterwards. Every other topic is
    /// what its file says and is handed back untouched.
    /// </remarks>
    /// <param name="topic">What was read off disc.</param>
    private HelpTopic Filled(HelpTopic topic) =>
        topic.Body.Contains(SystemKeysMark, StringComparison.Ordinal)
        || topic.Body.Contains(MenuKeysMark, StringComparison.Ordinal)
            ? topic with
            {
                Body = topic.Body
                    .Replace(SystemKeysMark, _keys.System, StringComparison.Ordinal)
                    .Replace(MenuKeysMark, _keys.Menu, StringComparison.Ordinal)
            }
            : topic;
}
