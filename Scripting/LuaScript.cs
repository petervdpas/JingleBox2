using System;
using System.Diagnostics;
using System.IO;
using JingleBox2.Diagnostics;
using MoonSharp.Interpreter;

namespace JingleBox2.Scripting;

/// <inheritdoc/>
/// <remarks>
/// MoonSharp, which is Lua 5.2 and runs in this process. The fence is three things in this
/// class and nothing anywhere else: <see cref="Allowed"/> is what a script may reach,
/// <see cref="TooLong"/> is how long one call may take, and <see cref="Working"/> is the switch
/// that goes off and stays off.
/// </remarks>
public sealed class LuaScript : ILuaScript
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

    /// <summary>The interpreter and everything the file has put in it, for this file alone.</summary>
    /// <remarks>
    /// One per file rather than one shared: two codecs that could see each other's globals
    /// would be two codecs that could break each other, and there is nothing they want to say
    /// to one another.
    /// </remarks>
    private readonly Script _script = new(Allowed);

    /// <summary>How long the last call took, which is the only thing the budget can be measured with.</summary>
    private readonly Stopwatch _clock = new();

    /// <summary>Made by <see cref="Open"/> once the file has been read, and no other way.</summary>
    private LuaScript(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
    }

    /// <inheritdoc/>
    public string Path { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
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

    /// <summary>The file's text, read on the way in and kept until <see cref="Start"/> runs it.</summary>
    /// <remarks>
    /// Read and run are two steps because the things the script is given have to be in place
    /// before its own body runs.
    /// </remarks>
    private string _source = "";

    /// <inheritdoc/>
    public void Give(string name, Func<ScriptExecutionContext, CallbackArguments, DynValue> what) =>
        _script.Globals[name] = DynValue.NewCallback(new CallbackFunction(what, name));

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool Has(string function) =>
        Working && _script.Globals.Get(function).Type == DataType.Function;

    /// <inheritdoc/>
    public DynValue Read(string name) => _script.Globals.Get(name);

    /// <inheritdoc/>
    /// <remarks>
    /// The budget is checked after the call rather than during it, since MoonSharp offers no
    /// way in from outside once a script is running. A call that overruns is allowed to finish
    /// and the script is switched off behind it.
    /// </remarks>
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

    /// <inheritdoc/>
    public DynValue NewTable() => DynValue.NewTable(_script);
}
