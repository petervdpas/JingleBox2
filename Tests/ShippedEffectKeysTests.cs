using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Everything a shipped effect's panel can turn is something the song writes down.
/// </summary>
/// <remarks>
/// A track's chain is saved from the engine's own list of keys rather than from its panel, so a
/// control added to the panel and the engine but not to that list turns, is heard, and comes back
/// at its default the next time the song is opened. Every tempo sync on every effect went missing
/// that way: a Phaser set to an eighth note was a Phaser running free after a reload.
/// </remarks>
public class ShippedEffectKeysTests
{
    private const int Rate = 48000;

    /// <summary>The rack folder beside the program, found by walking up from the test's output.</summary>
    private static string Shipped(string world)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", world))) at = at.Parent;

        return at is null ? "" : Path.Combine(at.FullName, "rack", world);
    }

    /// <summary>The engine a manifest names, built the way the registry builds it.</summary>
    /// <remarks>Without regard to case, as the registry reads it: a manifest says Delay for delay.</remarks>
    private static ISoundEffectEngine? EngineFor(SoundEffectProject project) => project.Engine.ToLowerInvariant() switch
    {
        SoundEffectEngines.Delayed => new Delay(Rate, project.Id),
        SoundEffectEngines.Filtered => new Sweep(Rate, project.Id),
        SoundEffectEngines.Driven => new Drive(Rate, project.Id),
        SoundEffectEngines.Shifted => new Shift(Rate, project.Id),
        SoundEffectEngines.Ringed => new Ring(Rate, project.Id),
        SoundEffectEngines.Widened => new Widen(Rate, project.Id),
        SoundEffectEngines.Phased => new Phase(Rate, project.Id),
        _ => null,
    };

    /// <summary>Every parameter a shipped effect's manifest declares is in its engine's saved keys.</summary>
    [Fact]
    public void Every_control_on_a_shipped_effect_is_saved_with_the_song()
    {
        var missing = new List<string>();
        int checkedEffects = 0;

        foreach (string folder in Directory.EnumerateDirectories(Shipped("effects")))
        {
            if (SoundEffectProject.Open(folder) is not { } project) continue;

            var engine = EngineFor(project);

            Assert.True(engine is not null, $"{project.Name} names an engine nothing builds: {project.Engine}");

            checkedEffects++;

            missing.AddRange(project.Parameters
                .Where(parameter => !engine!.Keys.Contains(parameter.Key))
                .Select(parameter => $"{project.Name}: {parameter.Key}"));
        }

        Assert.True(checkedEffects > 0, "No shipped effects were found to check.");
        Assert.Empty(missing);
    }
}
