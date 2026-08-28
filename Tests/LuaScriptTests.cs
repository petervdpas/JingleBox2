using System;
using System.IO;
using JingleBox2.Scripting;
using MoonSharp.Interpreter;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The Lua host, and the fence around it.
/// </summary>
/// <remarks>
/// A script arrives from somebody else. What it can reach is a list in one place, and the two
/// ways it can misbehave both end with it switched off rather than caught and shrugged at: a
/// codec that throws is producing wrong MIDI, and a hundred messages a second means a hundred
/// identical lines of log a second.
/// <para>
/// The engine is MoonSharp and the language is Lua 5.2, which means bit32.rshift and not
/// &gt;&gt;. A script that takes more than 20ms is switched off; that budget is what the slow
/// test below is written against.
/// </para>
/// <para>
/// Three groups here, in order: a script that runs and one that does not, then the two ways one
/// misbehaves, then the fence itself, which is the list of what a script may and may not reach
/// and what the host hands it.
/// </para>
/// </remarks>
public class LuaScriptTests : IDisposable
{
    /// <summary>A folder of its own per test class, so nothing here reads anybody's scripts.</summary>
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "jinglebox2-lua-" + Guid.NewGuid().ToString("N"));

    /// <summary>Makes the folder the scripts under test are written into.</summary>
    public LuaScriptTests() => Directory.CreateDirectory(_folder);

    /// <summary>
    /// Takes the folder away again, and does not care if the operating system is still holding
    /// something in it: a temporary folder left behind is not worth failing a suite over.
    /// </summary>
    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    /// <summary>Writes a script to a file and opens it, since one is only ever read off disc.</summary>
    private LuaScript? Written(string lua, string name = "test.lua")
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, lua);

        return LuaScript.Open(path);
    }

    /// <summary>The whole of the good path: opened, started, asked whether it has a function,
    /// and called for an answer.</summary>
    [Fact]
    public void A_script_that_reads_runs_and_answers()
    {
        var script = Written("function twice(n) return n * 2 end");

        Assert.NotNull(script);
        Assert.True(script!.Start());
        Assert.True(script.Working);
        Assert.True(script.Has("twice"));

        Assert.Equal(14, script.Call("twice", DynValue.NewNumber(7))!.Number);
    }

    /// <summary>
    /// A file that is not Lua opens, refuses to start, and is then not working, rather than
    /// throwing out of whatever was loading the controller folder.
    /// </summary>
    [Fact]
    public void A_script_that_will_not_parse_is_reported_and_left_alone()
    {
        var script = Written("this is not lua at all !!!");

        Assert.NotNull(script);
        Assert.False(script!.Start());
        Assert.False(script.Working);
    }

    /// <summary>A device with no script of its own is the ordinary case, so a missing file is
    /// nothing rather than an error.</summary>
    [Fact]
    public void A_file_that_is_not_there_is_nothing()
    {
        Assert.Null(LuaScript.Open(Path.Combine(_folder, "never written.lua")));
    }

    /// <summary>
    /// A script that throws is switched off and not asked again, whatever is asked of it.
    /// </summary>
    /// <remarks>
    /// A codec that throws is producing wrong MIDI, and it sits on a path that runs a hundred
    /// times a second, so catching and carrying on would be a hundred identical lines of log a
    /// second and a stream nobody can trust.
    /// </remarks>
    [Fact]
    public void A_script_that_throws_is_switched_off_rather_than_asked_again()
    {
        var script = Written("function bang() error('on purpose') end");

        script!.Start();

        Assert.Null(script.Call("bang"));
        Assert.False(script.Working);

        Assert.Null(script.Call("bang"));
    }

    /// <summary>
    /// A script that runs past the 20ms budget is switched off, one call after the fact.
    /// </summary>
    /// <remarks>
    /// It cannot be stopped once it is running, so the only defence is refusing it the next
    /// message. After the fact by one call, which is a hitch rather than a hang.
    /// <para>
    /// The loop is enough to pass the budget by a good margin and no more: a suite that takes
    /// half a minute to say the same thing is a suite nobody runs.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_script_that_will_not_stop_is_switched_off_too()
    {
        var script = Written("function slow() local n = 0 for i = 1, 3000000 do n = n + i end return n end");

        script!.Start();

        script.Call("slow");

        Assert.False(script.Working);
    }

    /// <summary>
    /// A script that does not do one of the jobs on offer says so and is not switched off for
    /// it: a codec that reads and does not write is an ordinary thing to write.
    /// </summary>
    [Fact]
    public void Asking_for_a_function_that_is_not_there_is_nothing()
    {
        var script = Written("x = 1");
        script!.Start();

        Assert.False(script.Has("nothing"));
        Assert.Null(script.Call("nothing"));
    }

    /// <summary>
    /// The fence: no file access, no operating system, and no way to load more code.
    /// </summary>
    /// <remarks>
    /// Every library a script may reach is named one at a time rather than taken from a preset,
    /// so a version of the engine that adds one does not quietly widen this.
    /// </remarks>
    [Theory]
    [InlineData("io")]
    [InlineData("os")]
    [InlineData("require")]
    [InlineData("load")]
    [InlineData("loadstring")]
    [InlineData("dofile")]
    public void What_a_script_cannot_reach(string name)
    {
        var script = Written($"function has() return {name} ~= nil end");

        script!.Start();

        Assert.False(script.Call("has")!.Boolean);
    }

    /// <summary>
    /// And what is left, which is enough to take bytes apart and put them back together:
    /// bit32 is there because this is Lua 5.2 and the shift operators are not.
    /// </summary>
    [Theory]
    [InlineData("string")]
    [InlineData("math")]
    [InlineData("table")]
    [InlineData("bit32")]
    public void And_what_it_can(string name)
    {
        var script = Written($"function has() return {name} ~= nil end");

        script!.Start();

        Assert.True(script.Call("has")!.Boolean);
    }

    /// <summary>
    /// What the host gives a script is in place before the script's own body runs, so a script
    /// can call it at the top level rather than only from inside a function.
    /// </summary>
    [Fact]
    public void Something_given_to_a_script_is_there_when_its_own_body_runs()
    {
        var script = Written("said = tell('hello')");

        int calls = 0;

        script!.Give("tell", (_, args) =>
        {
            calls++;

            return DynValue.NewString("got " + args[0].CastToString());
        });

        Assert.True(script.Start());
        Assert.Equal(1, calls);
        Assert.Equal("got hello", script.Read("said").CastToString());
    }
}
