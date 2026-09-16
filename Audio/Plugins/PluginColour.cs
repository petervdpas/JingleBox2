using System;
using System.IO;
using System.Text.Json;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The colour a plugin says it would like to be drawn in, where it says one.
/// </summary>
/// <remarks>
/// A file of ours inside the plugin, <c>Contents/Resources/JingleBox2.json</c>, holding nothing
/// but a colour. Nobody else's plugins have it and nobody else's hosts read it; ours do both,
/// which is the whole of why it can exist. Read off the disc rather than asked of the plugin, so
/// a list of plugins can be drawn in their own colours without loading a single one of them.
///
/// Anything unreadable is no colour rather than a fault. A plugin that says nothing is drawn the
/// way every plugin was drawn before this existed, which is the answer for all but three of them.
/// </remarks>
public static class PluginColour
{
    /// <summary>What the file is called, inside the bundle's resources.</summary>
    public const string FileName = "JingleBox2.json";

    /// <summary>The colour that plugin asks for, or null where it asks for none.</summary>
    /// <param name="bundlePath">The plugin as it lies on disc: a folder for VST3, a file for CLAP.</param>
    public static string? For(string? bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath)) return null;

        try
        {
            string folder = Directory.Exists(bundlePath)
                ? Path.Combine(bundlePath, "Contents", "Resources")
                : Path.GetDirectoryName(bundlePath) ?? "";

            string file = Path.Combine(folder, FileName);

            if (!File.Exists(file)) return null;

            using var read = JsonDocument.Parse(File.ReadAllText(file));

            if (!read.RootElement.TryGetProperty("colour", out var said)) return null;

            string? colour = said.GetString();

            return Looks(colour) ? colour : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Whether that is a colour rather than whatever else was in the file.</summary>
    /// <remarks>
    /// Checked here rather than left to the drawing, since a bad string reaching a brush is an
    /// exception in the middle of a layout, and this file comes from outside the application.
    /// </remarks>
    private static bool Looks(string? colour)
    {
        if (colour == null || (colour.Length != 7 && colour.Length != 9) || colour[0] != '#') return false;

        for (int at = 1; at < colour.Length; at++)
        {
            if (!Uri.IsHexDigit(colour[at])) return false;
        }

        return true;
    }
}
