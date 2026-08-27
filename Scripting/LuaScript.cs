using System;
using System.Diagnostics;
using System.IO;
using JingleBox2.Diagnostics;
using MoonSharp.Interpreter;

namespace JingleBox2.Scripting;

/// <summary>
/// One Lua file, loaded, fenced in, and answering for itself when it goes wrong.
/// </summary>
/// <remarks>
/// Why a language at all, when this application has spent its life describing things in JSON
/// instead: because the JSON stops working at exactly the point hardware support begins. A
/// machine's face is a fixed vocabulary of knobs and groups and the host draws what it is told.
/// A controller is not like that. One device assembles a fader out of two messages, another
/// wants a checksum, a third changes what nine of its controls mean when a button is pressed,
/// and a fourth counts its encoder backwards. Every DAW that has tried this arrived at the same
/// answer and none of them stayed declarative: Reason writes codecs in Lua, Ableton in Python,
/// Bitwig in Java and then JavaScript. A description format that has to cover that grows a
/// field per device until it is a programming language with no debugger.
///
/// So: JSON for what a device has, and this for what a device does, and the second one only
/// when the first is not enough.
///
/// Fenced in three ways, because a script arrives from somebody else and a person adding a
/// controller should not have to be trusted with the filesystem to do it.
///
/// <list type="bullet">
/// <item>The library it gets is written out below rather than named as a preset, so what a
/// script can reach is one list in one place. No io, no os, no require, no loading more code.</item>
/// <item>An error switches the script off rather than being caught and shrugged at. A codec
/// that throws is producing wrong MIDI, and a hundred messages a second means a hundred
/// identical lines of log a second.</item>
/// <item>A call that takes too long switches it off as well. There is no way to interrupt a
/// script mid-loop from outside, so the only defence is to refuse it the next message. It is
/// after the fact by one call, which is the difference between a hitch and a hang.</item>
/// </list>
/// </remarks>
public sealed class LuaScript
{
    /// <summary>
    /// Everything a script is allowed to touch, written out rather than named as a preset.
    /// </summary>
    /// <remarks>
    /// Deliberately a list you can read. A preset name says nothing about what it contains, and
    /// what somebody else's file may reach is exactly the thing that should not need looking up.
    /// Notably absent: IO, OS_System, OS_Time, Debug, and LoadMethods, which is the one that
    /// would let a script fetch more code and make the rest of this pointless.
    /// </remarks>
    private const CoreModules Allowed =
        CoreModules.Basic
        | CoreModules.GlobalConsts
        | CoreModules.TableIterators
        | CoreModules.Metatables
        | CoreModules.String
        | CoreModules.Table
        | CoreModules.ErrorHandling
        | CoreModules.Math
        | CoreModules.Bit32;

    /// <summary>How long one call may take before the script is considered broken.</summary>
    /// <remarks>
    /// Generous by two orders of magnitude. A codec does arithmetic on five numbers; anything
    /// near this is a loop that is not going to end.
    /// </remarks>
    private static readonly TimeSpan TooLong = TimeSpan.FromMilliseconds(20);

    private readonly Script _script = new(Allowed);
    private readonly Stopwatch _clock = new();

    private LuaScript(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
    }

    public string Path { get; }

    /// <summary>The file name, which is what a message about it should say.</summary>
    public string Name { get; }

    /// <summary>False once it has misbehaved. It is not asked again until it is reloaded.</summary>
    public bool Working { get; private set; }

    /// <summary>
    /// Reads a file and prepares it, without running it yet.
    /// </summary>
    /// <remarks>
    /// Two steps rather than one because the things the script is given have to be in place
    /// before its own body runs. A file whose first line calls <c>log</c> is a reasonable file.
    /// </remarks>
    public static LuaScript? Open(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var one = new LuaScript(path);
            one._source = File.ReadAllText(path);

            return one;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "script: cannot read '" + path + "': " + bad.Message);
            return null;
        }
    }

    private string _source = "";

    /// <summary>Puts something in the script's reach, under a name it can call.</summary>
    public void Give(string name, Func<ScriptExecutionContext, CallbackArguments, DynValue> what) =>
        _script.Globals[name] = DynValue.NewCallback(new CallbackFunction(what, name));

    /// <summary>Runs the file's own body. False when it will not even parse.</summary>
    public bool Start()
    {
        try
        {
            _script.DoString(_source, null, Name);
            Working = true;

            return true;
        }
        catch (InterpreterException bad)
        {
            Log.Write(LogArea.Midi, () => "script: '" + Name + "' will not load: " + bad.DecoratedMessage);
            return false;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "script: '" + Name + "' will not load: " + bad.Message);
            return false;
        }
    }

    /// <summary>True when the file defines a function of that name.</summary>
    public bool Has(string function) =>
        Working && _script.Globals.Get(function).Type == DataType.Function;

    /// <summary>Reads a global the file set, for the table a controller describes itself with.</summary>
    public DynValue Read(string name) => _script.Globals.Get(name);

    /// <summary>
    /// Calls one of the file's functions. Null when it is not there, or when it just broke.
    /// </summary>
    public DynValue? Call(string function, params DynValue[] with)
    {
        if (!Working) return null;

        var fn = _script.Globals.Get(function);
        if (fn.Type != DataType.Function) return null;

        try
        {
            _clock.Restart();
            var answer = _script.Call(fn, with);
            _clock.Stop();

            if (_clock.Elapsed > TooLong)
            {
                Working = false;

                Log.Write(LogArea.Midi, () =>
                    "script: '" + Name + "' took " + _clock.ElapsedMilliseconds + "ms in " + function
                    + " and has been switched off. It cannot be stopped once it is running, so the"
                    + " only thing that can be done is not to call it again");
            }

            return answer;
        }
        catch (InterpreterException bad)
        {
            Working = false;

            Log.Write(LogArea.Midi, () =>
                "script: '" + Name + "' failed in " + function + " and has been switched off: "
                + bad.DecoratedMessage);

            return null;
        }
        catch (Exception bad)
        {
            Working = false;
            Log.Write(LogArea.Midi, () => "script: '" + Name + "' failed in " + function + ": " + bad.Message);

            return null;
        }
    }

    /// <summary>A table to hand to a script, built against this script's own heap.</summary>
    public DynValue NewTable() => DynValue.NewTable(_script);
}
