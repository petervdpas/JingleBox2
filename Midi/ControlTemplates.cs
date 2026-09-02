using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Midi.Records;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class ControlTemplates : IControlTemplates
{
    /// <summary>What a target is called and which parameter, which the file and the page share.</summary>
    private readonly ILinkTargets _targets;

    /// <summary>Where the application keeps its things.</summary>
    private readonly IAppFolder _app;

    /// <summary>Writing a file whole, so a half-written one cannot replace a good one.</summary>
    private readonly ISafeFile _files;

    /// <summary>What the folder of templates is called under the application folder.</summary>
    private const string Templates = "templates";

    /// <summary>The extension a template is written with.</summary>
    public const string Extension = "jbtl";

    /// <summary>Reads and writes templates.</summary>
    /// <param name="targets">What a target is called, shared with the page so the two agree.</param>
    /// <param name="app">Where the application keeps its things.</param>
    /// <param name="files">How a file is written whole.</param>
    public ControlTemplates(ILinkTargets? targets = null, IAppFolder? app = null, ISafeFile? files = null)
    {
        _targets = targets ?? new LinkTargets();
        _app = app ?? new AppFolder();
        _files = files ?? new SafeFile();
    }

    /// <summary>How the file is laid out: indented, and named the way its own properties are.</summary>
    /// <remarks>
    /// Indented because a template is meant to be opened and read, and camel case because that
    /// is what the rest of this application's files look like from the outside.
    /// </remarks>
    private static readonly JsonSerializerOptions Layout = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc/>
    public string Folder()
    {
        string path = System.IO.Path.Combine(_app.Path(), Templates);

        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            Log.Write(LogArea.Midi, () => "templates: cannot make '" + path + "': " + ex.Message);
        }

        return path;
    }

    /// <inheritdoc/>
    public string FileName(ControlTemplate template)
    {
        if (template is null) return "template";

        string said = Plain(template.Controller) + "-" + Plain(template.Target.Name.Length > 0
            ? template.Target.Name
            : template.Target.Kind);

        return said.Trim('-') is { Length: > 0 } trimmed ? trimmed : "template";
    }

    /// <inheritdoc/>
    public ControlTemplate? Describe(string controller, IEnumerable<ControlMapping> links, Func<int, int, string>? named = null)
    {
        var all = links?.ToList() ?? new List<ControlMapping>();

        if (all.Count == 0) return null;

        string key = _targets.KeyOf(all[0]);

        if (all.Any(one => !string.Equals(_targets.KeyOf(one), key, StringComparison.Ordinal)))
        {
            Log.Write(LogArea.Midi, () =>
                "templates: refused to write links on more than one target as one template");

            return null;
        }

        var template = new ControlTemplate
        {
            Controller = controller ?? "",
            Target = new ControlTemplateTarget
            {
                Kind = _targets.KindOf(all[0]),
                Id = Strips(all[0]) ? "" : _targets.IdOf(all[0]),
                Name = _targets.TitleOf(all)
            }
        };

        foreach (var one in all.OrderBy(one => one.Channel).ThenBy(one => one.Cc))
            template.Controls.Add(new ControlTemplateControl
            {
                Control = named?.Invoke(one.Channel, one.Cc) ?? "",
                Channel = one.Channel,
                Cc = one.Cc,
                Parameter = _targets.ParameterOf(one),
                Name = one.Name,
                Pickup = _targets.Said(one.Pickup),
                Turn = _targets.Said(one.Turn),
                Track = Pinned(one) ? one.Track + 1 : 0,
                Strip = Strips(one) ? _targets.IdOf(one) : ""
            });

        return template;
    }

    /// <inheritdoc/>
    public ControlTemplateReading Take(ControlTemplate? template, IEnumerable<string>? ports = null, Func<string, string>? called = null)
    {
        var links = new List<ControlMapping>();

        if (template?.Controls is not { Count: > 0 })
            return new ControlTemplateReading(links, 0, template?.Controller ?? "", false);

        string wanted = template.Controller ?? "";
        string port = Port(wanted, ports, called);
        bool found = port.Length > 0;

        int skipped = 0;

        foreach (var entry in template.Controls)
        {
            var one = _targets.Point(
                template.Target?.Kind ?? "",
                entry.Strip is { Length: > 0 } strip ? strip : template.Target?.Id ?? "",
                entry.Parameter ?? "",
                template.Target?.Name ?? "",
                entry.Name ?? "");

            if (one is null)
            {
                skipped++;
                continue;
            }

            one.Device = found ? port : wanted;
            one.Channel = entry.Channel is >= 1 and <= 16 ? entry.Channel : 1;
            one.Cc = entry.Cc is >= 0 and <= 127 ? entry.Cc : 0;

            if (_targets.Pickup(entry.Pickup ?? "") is { } pickup) one.Pickup = pickup;
            if (_targets.Turn(entry.Turn ?? "") is { } turn) one.Turn = turn;

            if (entry.Track >= 1 && one.Kind is ControlKind.Device or ControlKind.Insert)
            {
                one.Scope = ControlScope.Fixed;
                one.Track = entry.Track - 1;
            }

            links.Add(one);
        }

        return new ControlTemplateReading(links, skipped, found ? port : wanted, found);
    }

    /// <inheritdoc/>
    public void Write(string path, ControlTemplate template)
    {
        if (template is null) return;

        template.JingleBox = ControlTemplate.Kind;
        template.Version = ControlTemplate.Now;

        _files.Write(path, stream => JsonSerializer.Serialize(stream, template, Layout));

        Log.Write(LogArea.Midi, () =>
            "templates: wrote " + template.Controls.Count + " controls for "
            + template.Target.Kind + " '" + template.Target.Name + "' to " + path);
    }

    /// <inheritdoc/>
    public ControlTemplate? Open(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);

            var template = JsonSerializer.Deserialize<ControlTemplate>(stream, Layout);

            if (template is null) return null;

            if (!string.Equals(template.JingleBox, ControlTemplate.Kind, StringComparison.OrdinalIgnoreCase))
            {
                Log.Write(LogArea.Midi, () =>
                    "templates: '" + path + "' says it is '" + template.JingleBox + "' rather than a control template");

                return null;
            }

            template.Target ??= new ControlTemplateTarget();
            template.Controls ??= new List<ControlTemplateControl>();

            return template;
        }
        catch (Exception ex)
        {
            Log.Write(LogArea.Midi, () => "templates: cannot read '" + path + "': " + ex.Message);

            return null;
        }
    }

    /// <summary>Whether a link on a machine or an effect is nailed to a track rather than following you.</summary>
    /// <param name="one">The link to ask about.</param>
    private static bool Pinned(ControlMapping one) =>
        one.Scope == ControlScope.Fixed && one.Kind is ControlKind.Device or ControlKind.Insert;

    /// <summary>
    /// Whether a link names a strip, which is what makes a template cover the whole mixer.
    /// </summary>
    /// <remarks>
    /// The mixer is one thing to point a controller at, so the target above says only that it is
    /// the mixer and each line says which strip it is on. Everything else names one thing and
    /// writes its id in the target, where it always was.
    /// </remarks>
    /// <param name="one">The link to ask about.</param>
    private static bool Strips(ControlMapping one) => one.Kind == ControlKind.Mix;

    /// <summary>
    /// Which port on this computer is the controller the file names, or nothing.
    /// </summary>
    /// <remarks>
    /// By what the profile calls a port rather than by the port's own spelling, since that is
    /// the whole reason the file names the controller and not the port. A controller with no
    /// profile is called by its port, so this still finds it when the two computers spell it the
    /// same way, which is what a file made and opened on one machine does.
    /// </remarks>
    /// <param name="controller">What the file named.</param>
    /// <param name="ports">The MIDI ports this computer has.</param>
    /// <param name="called">What a port's profile calls it.</param>
    private static string Port(string controller, IEnumerable<string>? ports, Func<string, string>? called)
    {
        if (controller.Length == 0 || ports is null) return "";

        foreach (string port in ports)
        {
            if (string.Equals(called?.Invoke(port) ?? port, controller, StringComparison.OrdinalIgnoreCase))
                return port;
        }

        return ports.FirstOrDefault(port =>
            string.Equals(port, controller, StringComparison.OrdinalIgnoreCase)) ?? "";
    }

    /// <summary>What is left of a name once everything a file system might object to is gone.</summary>
    /// <param name="said">The words to cut down.</param>
    private static string Plain(string said)
    {
        var kept = (said ?? "")
            .Select(letter => char.IsLetterOrDigit(letter) ? char.ToLowerInvariant(letter) : '-')
            .ToArray();

        string one = new(kept);

        while (one.Contains("--", StringComparison.Ordinal)) one = one.Replace("--", "-", StringComparison.Ordinal);

        return one.Trim('-');
    }
}
