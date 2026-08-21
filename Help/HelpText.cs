using System;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Help;

/// <summary>
/// Everything the app explains about itself, in one place, looked up by id.
/// </summary>
/// <remarks>
/// The ids are declared as constants and the table is written out in full, rather than being
/// built from a prefix and a name at the point of use. That way every id that exists appears
/// here as a literal, so it can be searched for, and a page asking for one that was never
/// written says so instead of showing an empty window.
///
/// Prose lives here rather than in the pages so the pages stay about their controls, and so
/// an explanation can be improved without touching a layout.
/// </remarks>
public static class HelpText
{
    // Declared, not composed. Every id in the app is one of these literals.
    public const string SettingsRecordingInput = "settings.recording-input";
    public const string SettingsMidiInput = "settings.midi-input";
    public const string SettingsEngine = "settings.engine";
    public const string SettingsPlugins = "settings.plugins";
    public const string SettingsPadMatrix = "settings.pad-matrix";
    public const string TrackerInstruments = "tracker.song-instruments";
    public const string TrackerEffects = "tracker.effects";
    public const string MixerStrips = "mixer.strips";

    private static readonly Dictionary<string, HelpTopic> Topics = new(StringComparer.Ordinal)
    {
        [SettingsRecordingInput] = new(
            SettingsRecordingInput,
            "Recording input",
            "Where the RECORD tab captures from.",
            """
            Pick the device the RECORD tab captures from. The choice is remembered between
            sessions.

            On Linux the RECORD tab can go further than a device: its own "Capture from" picker
            offers the programs that are playing, so a browser can be recorded on its own. That
            is PipeWire, which treats every stream as something that can be patched.

            On Windows the same picker offers each output through WASAPI loopback, which records
            everything that output is playing rather than one program.
            """),

        [SettingsMidiInput] = new(
            SettingsMidiInput,
            "MIDI input",
            "Which controller drives what.",
            """
            Tick what each controller drives. A pad box and a keyboard can be connected at the
            same time: one can fire pads while the other plays the tracker.

            Pad Mapping is where a pad controller learns its notes. It only applies to devices
            ticked for pads, since a keyboard playing the tracker has no mapping to learn.
            """),

        [SettingsEngine] = new(
            SettingsEngine,
            "Engine",
            "The rate the tracker, synth and plugins run at.",
            """
            What the tracker, the synth and any plugins on them run at.

            Following the output device is the right answer almost always: the audio is not
            resampled on its way out, and a plugin is told the rate it is really being fed at.
            A plugin built for one rate and fed another has its filters and timings in the wrong
            place, which is what the "samplerate mismatch" messages some plugins print are about.

            A rate cannot change while the app is running. Voices, envelopes, filters and every
            loaded plugin work their timings out from it once, so a change takes effect the next
            time the app starts.
            """),

        [SettingsPlugins] = new(
            SettingsPlugins,
            "Audio plugins",
            "CLAP and VST3 plugins this machine has.",
            """
            Plugins installed on this machine, in either of the two formats this app hosts.

            CLAP is the newer one, with a plain C interface. LSP, Surge and Vital ship it, and
            on Linux it lives in ~/.clap and /usr/lib/clap. VST3 is the one nearly everything
            ships, Serum included, and it lives in ~/.vst3 and /usr/lib/vst3.

            Effects from either format go in the same chain, side by side, on a pad or a tracker
            track. Instruments are a different thing: they take notes rather than audio, so they
            are kept out of effect chains and turned into tracker instruments instead, on the
            INSTRUMENTS page. Only VST3 instruments can be played so far.

            Windows plugins are not Linux plugins. A Windows VST3 holds a .dll and needs wine
            and yabridge to run at all; what is listed here is what runs natively.

            A plugin draws its own interface where it has one, in a window of its own that you
            can leave open while you work. The host's knobs are the fallback for a plugin that
            draws nothing, and are what a plugin gets on a platform where its window will not
            open. Plugin windows are X11 only so far.

            Every plugin runs in a process of its own, and so does the scan. A plugin that
            falls over takes nothing with it: the effect passes its audio through untouched or
            the instrument goes quiet, a note says which plugin stopped, and there is a button
            to start it again with the settings it had. Nothing else in the app notices.

            Scanning opens each plugin to ask what is inside it. They stay loaded until the app
            closes, on purpose: unloading plugin libraries after they have been used is what
            makes hosts crash.

            Folders of your own are searched before the standard ones, and are kept with the
            rest of the settings.
            """),

        [SettingsPadMatrix] = new(
            SettingsPadMatrix,
            "Pad matrix size",
            "How many pads, and in what shape.",
            """
            Rows and columns for the pad grid. Minimum 4 pads (2x2 or 1x4), maximum 16 (4x4 or
            2x8).

            Changing the matrix stops all playing audio and rebuilds the grid. Pad settings are
            kept where possible: a pad that still exists after the change keeps its sound, its
            colour and its volume.
            """),

        [TrackerInstruments] = new(
            TrackerInstruments,
            "Song instruments",
            "The sounds this song uses, and which track each is on.",
            """
            Drag one onto a track, anywhere in its column, to put it there. An instrument sits on
            one track only: to use the same sound twice, add it twice.

            A song holds a copy of every instrument it uses, so it still plays on a machine
            without your library. Opening a song rebinds those copies to the library by id, which
            is why editing an instrument on the INSTRUMENTS tab reaches every song that uses it.

            "Add to library" pushes a song's instrument the other way, so other songs can use it.
            "Remove from song" takes the slot out of this song and leaves the library alone.
            """),

        [TrackerEffects] = new(
            TrackerEffects,
            "Track effects",
            "The chain of plugins on the track the cursor is on.",
            """
            The effects on the track the cursor is on, in the order the audio goes through them.
            Moving the cursor to another track changes what this row is about.

            The plus adds an effect to the end of the chain. A box opens that plugin's controls
            in a window of its own, and its power button switches it off without taking it out,
            so it can be heard in and out. Right click a box to move it earlier or later, or to
            remove it.

            Chains are saved with the song. A plugin that is missing when a song is opened is
            named rather than passed over, and the rest of the chain still loads.
            """),

        [MixerStrips] = new(
            MixerStrips,
            "Mixer",
            "One strip per track: level, placement, mute, solo and ducking.",
            """
            One strip per track. Solo silences every track that is not soloed, and mute beats
            solo on the same strip.

            The side chain at the bottom of a strip ducks that track while another one sounds:
            pick the track to listen to, how far down this one goes, and how long it takes to
            come back. The attack is always fast, because a slow one leaves the kick fighting
            the track it is meant to be clearing room for.

            The mix is part of the song and is saved with it. Moves are heard straight away,
            even in the middle of a take.
            """)
    };

    /// <summary>The topic with that id, or null when nothing has been written for it.</summary>
    public static HelpTopic? Find(string? id) =>
        !string.IsNullOrWhiteSpace(id) && Topics.TryGetValue(id, out var topic) ? topic : null;

    /// <summary>Everything there is, for the help window's list.</summary>
    public static IReadOnlyList<HelpTopic> All { get; } =
        Topics.Values.OrderBy(topic => topic.Title, StringComparer.OrdinalIgnoreCase).ToList();
}
