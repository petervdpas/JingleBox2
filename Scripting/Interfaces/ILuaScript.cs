using System;
using MoonSharp.Interpreter;

namespace JingleBox2.Scripting.Interfaces;

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
/// The dialect is Lua 5.2, which is the one thing a person writing a file here has to know
/// before they write anything: it means <c>bit32.rshift</c> and not <c>&gt;&gt;</c>.
///
/// Fenced in three ways, because a script arrives from somebody else and a person adding a
/// controller should not have to be trusted with the filesystem to do it.
///
/// <list type="bullet">
/// <item>The library it gets is written out rather than named as a preset, so what a script can
/// reach is one list in one place. No io, no os, no require, no loading more code.</item>
/// <item>An error switches the script off rather than being caught and shrugged at. A codec
/// that throws is producing wrong MIDI, and a hundred messages a second means a hundred
/// identical lines of log a second.</item>
/// <item>A call that takes too long switches it off as well. There is no way to interrupt a
/// script mid-loop from outside, so the only defence is to refuse it the next message. It is
/// after the fact by one call, which is the difference between a hitch and a hang.</item>
/// </list>
///
/// Switched off stays off until the file is read again, which for a codec is the next save of
/// it. That is deliberate: a script that has misbehaved once is not asked politely a second
/// time, and the thing that puts it back is somebody editing it.
/// </remarks>
public interface ILuaScript
{
    /// <summary>Where the file is.</summary>
    string Path { get; }

    /// <summary>The file name, which is what a message about it should say.</summary>
    string Name { get; }

    /// <summary>False once it has misbehaved. It is not asked again until it is reloaded.</summary>
    bool Working { get; }

    /// <summary>
    /// Puts something in the script's reach, under a name it can call.
    /// </summary>
    /// <remarks>
    /// Everything a script can do to the world outside itself arrives this way, which is what
    /// makes the fence hold: the libraries say what it may compute with, and this says what it
    /// may touch. Given before <see cref="Start"/>, or a file whose first line calls one of
    /// them fails on that line.
    /// </remarks>
    void Give(string name, Func<ScriptExecutionContext, CallbackArguments, DynValue> what);

    /// <summary>Runs the file's own body. False when it will not even parse.</summary>
    bool Start();

    /// <summary>True when the file defines a function of that name.</summary>
    /// <remarks>
    /// Asked rather than assumed, since a file is allowed to answer only the questions it cares
    /// about and calling a function nobody wrote is a fault the script did not commit.
    /// </remarks>
    bool Has(string function);

    /// <summary>Reads a global the file set, for the table a controller describes itself with.</summary>
    DynValue Read(string name);

    /// <summary>
    /// Calls one of the file's functions. Null when it is not there, or when it just broke.
    /// </summary>
    /// <remarks>
    /// The two are one answer on purpose. A caller has the same thing to do either way, which is
    /// carry on with what it already had, and telling them apart would only invite somebody to
    /// treat a broken script as a special case rather than as an absent one.
    /// </remarks>
    DynValue? Call(string function, params DynValue[] with);

    /// <summary>
    /// A table to hand to a script, built against this script's own heap.
    /// </summary>
    /// <remarks>
    /// Its own, and not any table: a value made against one script and passed to another is a
    /// fault inside the interpreter rather than an error anybody can read.
    /// </remarks>
    DynValue NewTable();
}
