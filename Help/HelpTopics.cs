using System;
using System.Collections.Generic;
using System.IO;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Help.Interfaces;
using JingleBox2.Help.Records;

namespace JingleBox2.Help;

/// <inheritdoc/>
/// <remarks>
/// The reading is deliberately forgiving, since these are prose files rather than a format
/// anybody has to get right: a file with no heading is titled by its own id, one with no
/// paragraph under the heading has no summary, and one that will not open at all is written
/// down and passed over. One topic that will not read is one topic, not the whole help.
/// </remarks>
public sealed class HelpTopics : IHelpTopics
{
    /// <summary>What a topic file is called.</summary>
    public const string Extension = ".md";

    /// <inheritdoc/>
    public IReadOnlyList<HelpTopic> In(string folder)
    {
        var topics = new List<HelpTopic>();

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return topics;

        foreach (string file in Directory.GetFiles(folder, "*" + Extension))
        {
            try
            {
                topics.Add(Read(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file)));
            }
            catch (Exception bad)
            {
                Log.Write(LogArea.App, () => "help: " + Path.GetFileName(file) + " would not read: " + bad.Message);
            }
        }

        return topics;
    }

    /// <summary>
    /// One file, split into the three things a topic is.
    /// </summary>
    /// <remarks>
    /// The heading and the summary are taken off the front rather than left in the body, or the
    /// window would draw the title twice: once in its own header and again at the top of the
    /// page under it.
    /// </remarks>
    /// <param name="id">The file's name, which is the topic's id.</param>
    /// <param name="text">What is in it.</param>
    public HelpTopic Read(string id, string text)
    {
        var lines = new List<string>((text ?? "").Replace("\r\n", "\n").Split('\n'));

        string title = Title(lines);
        string summary = Summary(lines);

        return new HelpTopic(id, title.Length > 0 ? title : id, summary, string.Join("\n", lines).Trim());
    }

    /// <summary>
    /// The first heading, taken off the front, or nothing when the file does not begin with one.
    /// </summary>
    /// <param name="lines">The file, which loses what is read.</param>
    private string Title(List<string> lines)
    {
        while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);

        if (lines.Count == 0 || !lines[0].TrimStart().StartsWith('#')) return "";

        string line = lines[0].Trim();

        lines.RemoveAt(0);

        return line.TrimStart('#').Trim();
    }

    /// <summary>
    /// The paragraph under the heading, taken off the front.
    /// </summary>
    /// <remarks>
    /// One paragraph and no more, since this is the line a list row and a tooltip show and both
    /// have room for a sentence. It stops at the blank line, so the rest of the file is the page
    /// whatever is in it.
    /// </remarks>
    /// <param name="lines">The file, which loses what is read.</param>
    private string Summary(List<string> lines)
    {
        while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);

        var said = new List<string>();

        while (lines.Count > 0 && lines[0].Trim().Length > 0)
        {
            if (lines[0].TrimStart().StartsWith('#')) break;

            said.Add(lines[0].Trim());

            lines.RemoveAt(0);
        }

        return string.Join(" ", said);
    }
}
