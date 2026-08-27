using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Diagnostics;
using JingleBox2.Midi;
using JingleBox2.Scripting;
using MoonSharp.Interpreter;

namespace JingleBox2.Controllers;

/// <summary>
/// The scripts that stand between a controller and the rest of the program.
/// </summary>
/// <remarks>
/// A codec is one file per controller, and its whole job is to turn what a device actually
/// sends into something this application already understands. It sits after the wire and before
/// the routing, so everything downstream carries on knowing nothing about any particular
/// hardware, which is the property worth protecting: a device nobody has written a file for
/// still works, because a message nothing translates is passed through untouched.
///
/// That is the point of doing it here rather than inside the routers. A codec cannot add a
/// feature and cannot take one away. It can only say that these bytes mean those bytes, which
/// is a small enough thing to hand to a stranger's file.
///
/// Two folders, as machines have two: beside the program is what ships and is never written to,
/// under the application folder is what this installation has. The first run fills the second
/// from the first. Somebody who deletes a codec has deleted it, and can take it again.
///
/// Matched on the port's name for now. Identity is the better key, since a MiniLab answers a
/// universal identity request with the same eleven bytes on every operating system while its
/// port is called something different on each, and moving the match onto that is the next thing
/// this wants. See docs/hardware-integration.md.
/// </remarks>
public sealed class ControllerCodecs : IDisposable
{
    /// <summary>Where a controller's files live. Shared with the profiles beside them.</summary>
    public static string Installed => ControllerFolder.Installed;

    private readonly IMidiService _midi;
    private readonly object _lock = new();
    private readonly List<Codec> _codecs = new();

    /// <summary>What was decided for a device, so the list is not walked per message.</summary>
    private readonly Dictionary<string, Codec?> _decided = new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _watch;

    public ControllerCodecs(IMidiService midi)
    {
        _midi = midi;

        ControllerFolder.FirstRun();
        Reload();
        Watch();
    }

    /// <summary>One file, and the device it turned out to be about.</summary>
    private sealed class Codec
    {
        public LuaScript Script = null!;
        public string Called = "";
        public string Matches = "";

        /// <summary>The port it is answering for at this moment, for anything it sends back.</summary>
        public string Device = "";
    }

    /// <summary>
    /// Reads every codec again, from scratch.
    /// </summary>
    /// <remarks>
    /// Called at startup and on every save of a file in the folder, which is the difference
    /// between writing one of these and enjoying writing one of these. A person adding a
    /// controller should edit, touch the knob, and see. Not edit, restart, replug, remember
    /// what they were testing.
    /// </remarks>
    public void Reload()
    {
        var found = new List<Codec>();

        try
        {
            if (Directory.Exists(Installed))
                foreach (string file in Directory.GetFiles(Installed, "*.lua").OrderBy(f => f, StringComparer.Ordinal))
                    if (Take(file) is { } one) found.Add(one);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "codecs: cannot read '" + Installed + "': " + bad.Message);
        }

        lock (_lock)
        {
            _codecs.Clear();
            _codecs.AddRange(found);
            _decided.Clear();
        }

        Log.Write(LogArea.Midi, () => "codecs: " + found.Count + " loaded from '" + Installed + "'");
    }

    /// <summary>Loads one file and asks it what it is about.</summary>
    private Codec? Take(string path)
    {
        var script = LuaScript.Open(path);
        if (script is null) return null;

        var codec = new Codec { Script = script, Called = script.Name };

        script.Give("log", (_, args) =>
        {
            string said = args.Count > 0 ? args[0].CastToString() ?? "" : "";
            Log.Write(LogArea.Midi, () => "codec '" + script.Name + "': " + said);

            return DynValue.Nil;
        });

        // Bytes back to whichever port this codec is answering for at the moment. Taken from
        // the codec rather than from the script, because a script that could name a port could
        // write down somebody else's.
        script.Give("send", (_, args) =>
        {
            if (codec.Device.Length == 0) return DynValue.False;

            var bytes = Bytes(args);
            if (bytes is null || bytes.Length == 0) return DynValue.False;

            return DynValue.NewBoolean(_midi.Send(codec.Device, bytes));
        });

        if (!script.Start()) return null;

        var said = script.Read("controller");
        string called = script.Name;
        string matches = Path.GetFileNameWithoutExtension(path);

        if (said.Type == DataType.Table)
        {
            if (said.Table.Get("name").CastToString() is { Length: > 0 } name) called = name;
            if (said.Table.Get("matches").CastToString() is { Length: > 0 } like) matches = like;
        }

        codec.Called = called;
        codec.Matches = matches;

        Log.Write(LogArea.Midi, () =>
            "codec: '" + script.Name + "' is " + called + ", for ports like '" + matches + "'");

        return codec;
    }

    /// <summary>A list of numbers, or one table of them, as bytes.</summary>
    private static byte[]? Bytes(CallbackArguments args)
    {
        if (args.Count == 1 && args[0].Type == DataType.Table)
        {
            var table = args[0].Table;
            var made = new byte[table.Length];

            for (int at = 0; at < made.Length; at++)
                made[at] = (byte)(int)table.Get(at + 1).CastToNumber().GetValueOrDefault();

            return made;
        }

        var one = new byte[args.Count];

        for (int at = 0; at < args.Count; at++)
        {
            if (args[at].CastToNumber() is not { } number) return null;
            one[at] = (byte)(int)number;
        }

        return one;
    }

    /// <summary>
    /// What the application should read instead of what arrived. Null when it was swallowed.
    /// </summary>
    /// <remarks>
    /// The message itself when nothing translates it, which is the ordinary case and the one
    /// that must stay free. A device with no codec pays one dictionary lookup.
    /// </remarks>
    public MidiMessage? Read(MidiMessage message)
    {
        if (message is null) return null;

        var codec = For(message.Device);
        if (codec is null || !codec.Script.Working) return message;

        var told = codec.Script.NewTable();
        var table = told.Table;

        table.Set("device", DynValue.NewString(message.Device));
        table.Set("type", DynValue.NewString(Word(message.Type)));
        table.Set("channel", DynValue.NewNumber(message.Channel));
        table.Set("number", DynValue.NewNumber(message.Value));
        table.Set("value", DynValue.NewNumber(message.Data));
        table.Set("on", DynValue.NewBoolean(message.IsOn));

        codec.Device = message.Device;

        var answer = codec.Script.Call("midi", told);

        // Nothing said, or the script is not interested: it stands as it arrived. This is the
        // path a codec takes for every message it does not care about, so it is the cheap one.
        if (answer is null || answer.IsNilOrNan()) return message;

        // Said so in as many words: true keeps it, false eats it.
        if (answer.Type == DataType.Boolean) return answer.Boolean ? message : null;

        if (answer.Type != DataType.Table) return message;

        return Made(message, answer.Table);
    }

    /// <summary>A message built from what the script handed back, with the rest left alone.</summary>
    private static MidiMessage? Made(MidiMessage was, Table table)
    {
        string kind = table.Get("type").CastToString() ?? Word(was.Type);

        var type = kind switch
        {
            "note" => MidiMessageType.Note,
            "cc" => MidiMessageType.ControlChange,
            "bend" => MidiMessageType.PitchBend,
            _ => (MidiMessageType?)null
        };

        if (type is null) return was;

        int channel = Number(table, "channel", was.Channel);
        int number = Number(table, "number", was.Value);
        int value = Number(table, "value", was.Data);

        bool on = table.Get("on").Type == DataType.Boolean
            ? table.Get("on").Boolean
            : value > 0;

        return new MidiMessage
        {
            Device = was.Device,
            Type = type.Value,
            Channel = channel,
            Value = number,
            Data = value,
            IsOn = type == MidiMessageType.PitchBend ? false : on
        };
    }

    private static int Number(Table table, string key, int otherwise) =>
        table.Get(key).CastToNumber() is { } number ? (int)number : otherwise;

    private static string Word(MidiMessageType type) => type switch
    {
        MidiMessageType.Note => "note",
        MidiMessageType.ControlChange => "cc",
        MidiMessageType.PitchBend => "bend",
        _ => "other"
    };

    /// <summary>The codec for a device, worked out once and remembered.</summary>
    private Codec? For(string device)
    {
        lock (_lock)
        {
            if (_decided.TryGetValue(device, out var known)) return known;

            Codec? found = _codecs.FirstOrDefault(one => ControllerFolder.Like(one.Matches, device));
            _decided[device] = found;

            if (found is not null)
                Log.Write(LogArea.Midi, () => "codecs: '" + device + "' is read by " + found.Called);

            return found;
        }
    }

    /// <summary>
    /// Watches the folder, so editing a controller file is editing a file and not a restart.
    /// </summary>
    /// <remarks>
    /// Every file in it, not only the codecs: the profile beside a codec is edited by the same
    /// person in the same sitting, and a folder where one half reloads and the other needs a
    /// restart is a folder nobody can hold in their head. This class owns the only watcher on
    /// that folder, so it tells the profiles as well.
    /// </remarks>
    private void Watch()
    {
        try
        {
            if (!Directory.Exists(Installed)) return;

            _watch = new FileSystemWatcher(Installed)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watch.Changed += OnFolderChanged;
            _watch.Created += OnFolderChanged;
            _watch.Deleted += OnFolderChanged;
            _watch.Renamed += OnFolderChanged;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "codecs: not watching '" + Installed + "': " + bad.Message);
        }
    }

    private DateTime _last = DateTime.MinValue;

    private void OnFolderChanged(object? sender, FileSystemEventArgs e)
    {
        // An editor saving a file makes several of these. Anything inside a moment of the last
        // one is the same save still happening.
        var now = DateTime.UtcNow;
        if (now - _last < TimeSpan.FromMilliseconds(250)) return;
        _last = now;

        try
        {
            Reload();
            ControllerProfiles.Reload();
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "controllers: reload failed: " + bad.Message);
        }
    }

    public void Dispose()
    {
        _watch?.Dispose();
        _watch = null;
    }
}
