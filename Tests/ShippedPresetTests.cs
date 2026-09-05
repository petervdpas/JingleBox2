using System;
using System.IO;
using System.Linq;
using JingleBox2.SoundDevices.SoundEffects;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The presets that ship beside the program, read off the disc rather than believed.
/// </summary>
/// <remarks>
/// A preset that ships is content, and content goes wrong the way content does: a key spelled
/// differently from the one on the face, a value past the end of a control, a file somebody
/// edited by hand and left as not quite JSON. None of that shows up until somebody picks the
/// preset and hears nothing change, so the folder is walked here with the same reader the
/// application uses.
///
/// It reads the shipped copy in the repository rather than the installed one, because that is the
/// copy this repository is answerable for and the installed one is whatever the person running it
/// has since done.
/// </remarks>
public class ShippedPresetTests
{
    /// <summary>The shelf, which is what the application reads them with.</summary>
    private readonly SoundEffectPresets _shelf = new();

    /// <summary>Where the shipped effects are, walking up out of the test's own output.</summary>
    private static string Shipped
    {
        get
        {
            var at = new DirectoryInfo(AppContext.BaseDirectory);

            while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", "effects")))
                at = at.Parent;

            return at is null ? "" : Path.Combine(at.FullName, "rack", "effects");
        }
    }

    /// <summary>
    /// The shipped folder is where the test expects it.
    /// </summary>
    /// <remarks>
    /// Said out loud rather than skipped past. A test that quietly passes when it cannot find the
    /// thing it is about is a test that reports nothing for the rest of its life, which is the
    /// same fault as a guard that was never exercised.
    /// </remarks>
    [Fact]
    public void The_shipped_effects_are_where_they_are_looked_for()
    {
        Assert.True(Directory.Exists(Shipped), "rack/effects was not found above " + AppContext.BaseDirectory);
        Assert.NotEmpty(Directory.GetDirectories(Shipped));
    }

    /// <summary>Every effect that ships brings presets, and every one of them reads.</summary>
    /// <remarks>
    /// The count is not pinned. What matters is that there are some and that each is whole, since
    /// pinning it would make adding one a test to fix rather than a file to write.
    ///
    /// Named one by one rather than walked, unlike the two tests below, because this one is
    /// about what is missing: an effect that shipped without its presets folder would be walked
    /// straight past by a loop over what is there.
    /// </remarks>
    [Theory]
    [InlineData("EchoBox")]
    [InlineData("Sweeper")]
    [InlineData("Roaster")]
    public void Every_shipped_effect_brings_presets(string name)
    {
        string folder = Path.Combine(Shipped, name);

        Assert.True(Directory.Exists(folder), name + " is not where it ships");

        var effect = SoundEffectProject.Open(folder);

        Assert.NotNull(effect);

        var presets = _shelf.For(effect);

        Assert.NotEmpty(presets);
        Assert.Equal(
            Directory.GetFiles(Path.Combine(folder, SoundEffectProject.PresetsFolder), "*.json").Length,
            presets.Count);
    }

    /// <summary>
    /// Every shipped preset sets every control the effect has.
    /// </summary>
    /// <remarks>
    /// A key that does not match one on the face is dropped as the file is read, silently and
    /// correctly, so a typo in a shipped preset is a control that quietly stays where it was.
    /// That is exactly the fault nobody notices, which is why it is counted here.
    /// </remarks>
    [Fact]
    public void Every_shipped_preset_sets_every_control()
    {
        Assert.True(Directory.Exists(Shipped));

        foreach (string folder in Directory.EnumerateDirectories(Shipped))
        {
            if (SoundEffectProject.Open(folder) is not { } effect) continue;

            foreach (var preset in _shelf.For(effect))
                Assert.Equal(
                    effect.Parameters.Select(one => one.Key).OrderBy(one => one, StringComparer.Ordinal),
                    preset.Settings.Keys.OrderBy(one => one, StringComparer.Ordinal));
        }
    }

    /// <summary>Two shipped presets never share a name, which a picker could not tell apart.</summary>
    [Fact]
    public void No_two_shipped_presets_share_a_name()
    {
        Assert.True(Directory.Exists(Shipped));

        foreach (string folder in Directory.EnumerateDirectories(Shipped))
        {
            if (SoundEffectProject.Open(folder) is not { } effect) continue;

            var names = _shelf.For(effect).Select(one => one.Name).ToList();

            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
