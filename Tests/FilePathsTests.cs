using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using System;
using System.IO;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The rule that decides two paths are one file, asked in both dialects on one machine.
/// </summary>
/// <remarks>
/// The point of these is that they run identically on Linux and on Windows. The rule used to be
/// read off the operating system inside the class, so the Windows answer could only be checked
/// on Windows and the Linux answer only on Linux, and the half of this application that keys
/// recordings by path is exactly the half where getting it wrong says nothing and shows up
/// later as a chop that stopped being a chop.
/// </remarks>
public class FilePathsTests
{
    /// <summary>What a Windows or macOS volume decides.</summary>
    private static IFilePaths Insensitive => new FilePaths(StringComparison.OrdinalIgnoreCase);

    /// <summary>What a Linux volume decides.</summary>
    private static IFilePaths Sensitive => new FilePaths(StringComparison.Ordinal);

    /// <summary>Two spellings of one name are one file where case is ignored, and two where it is not.</summary>
    [Fact]
    public void CaseDecidesWhetherTwoSpellingsAreOneFile()
    {
        Assert.True(Insensitive.Same("/takes/KICK.wav", "/takes/kick.WAV"));
        Assert.False(Sensitive.Same("/takes/KICK.wav", "/takes/kick.WAV"));
    }

    /// <summary>Left out, the rule is the one this machine really has.</summary>
    [Fact]
    public void WithoutARuleItReadsTheMachine()
    {
        var here = new FilePaths();

        Assert.Equal(
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal,
            here.Comparison);
    }

    /// <summary>Nothing and the empty name are the same name, and neither throws.</summary>
    [Fact]
    public void NothingIsTheEmptyName()
    {
        Assert.True(Sensitive.Same(null, ""));
        Assert.True(Sensitive.Same(null, null));
        Assert.Equal("", Sensitive.Full(null));
        Assert.Equal("", Sensitive.Full(""));
    }

    /// <summary>The comparer agrees with the comparison, which is what a set keyed by paths rests on.</summary>
    [Fact]
    public void TheComparerAgreesWithTheComparison()
    {
        Assert.True(Insensitive.Comparer.Equals("/takes/A.wav", "/takes/a.wav"));
        Assert.False(Sensitive.Comparer.Equals("/takes/A.wav", "/takes/a.wav"));

        Assert.Equal(
            Insensitive.Comparer.GetHashCode("/takes/A.wav"),
            Insensitive.Comparer.GetHashCode("/takes/a.wav"));
    }

    /// <summary>A trailing separator does not make two files out of one.</summary>
    [Fact]
    public void ATrailingSeparatorIsNotADifferentFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "jinglebox-paths");

        Assert.True(Sensitive.SameFile(folder, folder + Path.DirectorySeparatorChar));
    }

    /// <summary>Two ways of writing one path reach the same file once they are resolved.</summary>
    [Fact]
    public void ResolvingFindsTheSameFileWrittenTwoWays()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "jinglebox-paths"));
        string roundabout = Path.Combine(root, "inner", "..", "take.wav");

        Assert.True(Sensitive.SameFile(Path.Combine(root, "take.wav"), roundabout));
        Assert.False(Sensitive.Same(Path.Combine(root, "take.wav"), roundabout));
    }

    /// <summary>A name no file could have is handed back as it stands rather than thrown over.</summary>
    /// <remarks>
    /// Equal to itself and to nothing else, which is the only honest answer available: there is
    /// no file to resolve it to. The character is a null, which no file system anywhere accepts.
    /// </remarks>
    [Fact]
    public void ANameNoFileCouldHaveComesBackUntouched()
    {
        string impossible = "/takes/ki\0ck.wav";

        Assert.Equal(impossible, Sensitive.Full(impossible));
        Assert.True(Sensitive.SameFile(impossible, impossible));
    }
}
