using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Help.Records;
using JingleBox2.Help.Interfaces;

namespace JingleBox2.Help;

/// <inheritdoc/>
public sealed class HelpText : IHelpText
{
    /// <summary>Which device a take is recorded from, and what a loopback is.</summary>
    public const string SettingsRecordingInput = "settings.recording-input";
    /// <summary>Which ports the pads and the tracker listen to, and what each one is for.</summary>
    public const string SettingsMidiInput = "settings.midi-input";
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

    /// <summary>Every topic there is, by its id.</summary>
    private readonly Dictionary<string, HelpTopic> Topics = new(StringComparer.Ordinal)
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
            "The rate, the buffer, and how the sound is kept fed.",
            """
            The rate is what the tracker, the synth and any plugins on them run at.

            Following the output device is the right answer almost always: the audio is not
            resampled on its way out, and a plugin is told the rate it is really being fed at.
            A plugin built for one rate and fed another has its filters and timings in the wrong
            place, which is what the "samplerate mismatch" messages some plugins print are about.

            A rate cannot change while the app is running. Voices, envelopes, filters and every
            loaded plugin work their timings out from it once, so a change takes effect the next
            time the app starts. It is the only setting here that waits.

            The output buffer is how much audio the sound card holds ahead of what you hear, so
            it is also the latency: what is playing was mixed that long ago, and it is how long a
            key waits before it sounds. Small is tighter to play and gives the mixing less room to
            be late in; too small for the machine and what comes out has holes in it. It is shown
            in frames, which is what every other audio application calls it, with the milliseconds
            beside it, since 512 frames is 12 ms at 44100 and 11 at 48000.

            How often it is topped up and how many threads do the topping go with it. One thread
            fills every stream in the application in turn, so a pad decoding a file can delay the
            tracker; more than one lets a slow stream stop holding up the others. Past four they
            wake to look at buffers that are already full.

            The plugin cushion is the same question at the other end. A plugin runs in a process
            of its own and every block it plays is a message out and a message back, made from the
            thread that has milliseconds to fill a buffer. A cushion moves that work onto a thread
            of its own, so a plugin being late eats into the queue instead of into the output, and
            it costs exactly what it says between playing a note and hearing it.

            Those four take effect at once: the output is closed and opened again as you change
            them, so the right value can be found by listening rather than by restarting between
            guesses.

            **If the sound goes strange after changing one, restart the app.** Reopening the
            output while everything else is still running is not the same as starting clean:
            plugins are still loaded, threads are already going, and the sound card has been
            handed back and taken again. The setting itself is remembered, so a restart costs
            nothing but the wait, and it is worth trying before concluding that a value is bad.
            """),

        [SettingsLog] = new(
            SettingsLog,
            "Log",
            "What the app writes down about itself when asked.",
            """
            Off, nothing is written and nothing is slowed down. On, the app writes what it is
            doing to jinglebox.log, next to the settings, and so does every process a plugin
            runs in. Each line carries the time, what it is about, and which process it came
            from, so a plugin falling over is next to what the app was doing at the time.

            The way to use it is to turn it on, do the thing that goes wrong, turn it off, and
            read the file. Leaving it on is not harmful: the file starts again from empty when
            it reaches a few megabytes, and the one before it is kept alongside.
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

            "Use extended pad matrix" raises that to 32, for a screen with the room for them.
            It is a switch of its own because a grid of 32 is a different instrument from a grid
            of 8, and not somewhere to arrive by holding an arrow key down. Turning it off again
            leaves a big grid that is already in force alone; it only refuses the next one.

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

            A song holds a copy of every instrument it uses, so it does not need your library to
            open. It does need the machines those instruments are on: one that is not installed
            here makes no sound until you add it under SETTINGS, and the song says which when it
            opens. Opening a song rebinds those copies to the rack by id, which is why editing an
            instrument on the INSTRUMENTS tab reaches every song that uses it.

            "Add to library" pushes a song's instrument the other way, so other songs can use it.
            "Remove from song" takes the slot out of this song and leaves the rack alone.
            """),

        [TrackerEffects] = new(
            TrackerEffects,
            "Track effects",
            "The chain of plugins on the track the cursor is on.",
            """
            The effects on the track the cursor is on, in the order the audio goes through them.
            Moving the cursor to another track changes what this row is about.

            When the track plays a plugin instrument, that plugin is the first box in the row,
            because that is where it is in the audio: it makes the sound and everything after
            works on what it made. Opening it gives you its own interface, and what you turn
            there is what the pattern plays. Its sound is written into the song when you save.

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

    /// <inheritdoc/>
    public HelpTopic? Find(string? id) =>
        !string.IsNullOrWhiteSpace(id) && Topics.TryGetValue(id, out var topic) ? topic : null;

    /// <inheritdoc/>
    /// <remarks>
    /// Worked out when it is asked for rather than when one of these is made, because the table
    /// it is built from is static and a field initialiser cannot reach it. Sorting nine entries
    /// is not worth keeping.
    /// </remarks>
    public IReadOnlyList<HelpTopic> All =>
        Topics.Values.OrderBy(topic => topic.Title, StringComparer.OrdinalIgnoreCase).ToList();
}
