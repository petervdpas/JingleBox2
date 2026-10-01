using System.Linq;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>Starting the application again the way it was started.</summary>
/// <remarks>
/// An installed copy and <c>dotnet run</c> are the program itself, and run as themselves again;
/// started as <c>dotnet JingleBox2.dll</c>, it is the runtime that has to be started, with the same
/// library. Whatever followed on the command line is handed on.
/// </remarks>
public class RelaunchTests
{
    /// <summary>The rule under test.</summary>
    private readonly IRelaunch _relaunch = new Relaunch();

    /// <summary>The program itself runs as itself again, with what followed it.</summary>
    [Fact]
    public void The_program_runs_itself_again()
    {
        var start = _relaunch.Again("/opt/JingleBox2/JingleBox2", new[] { "/opt/JingleBox2/JingleBox2", "--song", "Vst-Fiesta" });

        Assert.Equal("/opt/JingleBox2/JingleBox2", start.FileName);
        Assert.Equal(new[] { "--song", "Vst-Fiesta" }, start.ArgumentList.ToArray());
    }

    /// <summary>Started through the runtime, the runtime is started again with the same library.</summary>
    [Theory]
    [InlineData("/usr/share/dotnet/dotnet")]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    public void Through_the_runtime_the_runtime_runs_the_library_again(string runtime)
    {
        var start = _relaunch.Again(runtime, new[] { "/home/me/JingleBox2.dll", "--song", "x" });

        Assert.Equal(runtime, start.FileName);
        Assert.Equal(new[] { "/home/me/JingleBox2.dll", "--song", "x" }, start.ArgumentList.ToArray());
    }

    /// <summary>A Windows build of the program runs itself again like any other.</summary>
    [Fact]
    public void A_windows_build_runs_itself_again()
    {
        var start = _relaunch.Again(@"C:\Program Files\JingleBox2\JingleBox2.exe", new[] { @"C:\Program Files\JingleBox2\JingleBox2.dll" });

        Assert.Equal(@"C:\Program Files\JingleBox2\JingleBox2.exe", start.FileName);
        Assert.Empty(start.ArgumentList);
    }

    /// <summary>It starts on its own, not as a child that goes when this one does.</summary>
    [Fact]
    public void It_starts_on_its_own()
    {
        var start = _relaunch.Again("/opt/JingleBox2/JingleBox2", new[] { "/opt/JingleBox2/JingleBox2" });

        Assert.False(start.RedirectStandardOutput);
        Assert.False(start.RedirectStandardError);
    }
}
