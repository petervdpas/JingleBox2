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
/// </remarks>
public class LuaScriptTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "jinglebox2-lua-" + Guid.NewGuid().ToString("N"));

    public LuaScriptTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    private LuaScript? Written(string lua, string name = "test.lua")
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, lua);

        return LuaScript.Open(path);
    }

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

    [Fact]
    public void A_script_that_will_not_parse_is_reported_and_left_alone()
    {
        var script = Written("this is not lua at all !!!");

        Assert.NotNull(script);
        Assert.False(script!.Start());
        Assert.False(script.Working);
    }

    [Fact]
    public void A_file_that_is_not_there_is_nothing()
    {
        Assert.Null(LuaScript.Open(Path.Combine(_folder, "never written.lua")));
    }

    [Fact]
    public void A_script_that_throws_is_switched_off_rather_than_asked_again()
    {
        var script = Written("function bang() error('on purpose') end");

        script!.Start();

        Assert.Null(script.Call("bang"));
        Assert.False(script.Working);

        // And not asked again, whatever is asked of it.
        Assert.Null(script.Call("bang"));
    }

    [Fact]
    public void A_script_that_will_not_stop_is_switched_off_too()
    {
        // Enough to pass the budget by a good margin and no more: a suite that takes half a
        // minute to say the same thing is a suite nobody runs.
        var script = Written("function slow() local n = 0 for i = 1, 3000000 do n = n + i end return n end");

        script!.Start();

        script.Call("slow");

        // It cannot be stopped once it is running, so the only defence is refusing it the next
        // message. After the fact by one call, which is a hitch rather than a hang.
        Assert.False(script.Working);
    }

    [Fact]
    public void Asking_for_a_function_that_is_not_there_is_nothing()
    {
        var script = Written("x = 1");
        script!.Start();

        Assert.False(script.Has("nothing"));
        Assert.Null(script.Call("nothing"));
    }

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
