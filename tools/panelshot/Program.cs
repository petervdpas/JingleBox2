using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Views;

namespace JingleBox2.Tools.PanelShot;

/// <summary>
/// Whatever the panel asks for, answered from the parameter's own default.
/// </summary>
/// <remarks>
/// A picture of a panel is a picture of where its controls stand rather than of what they are
/// set to, so nothing here is stored and nothing is played. Writes are kept all the same, since
/// a control that cannot be written to is one that argues with itself on the way in.
/// </remarks>
internal sealed class Standing(IReadOnlyList<Parameter> parameters) : IPanelValues
{
    private readonly Dictionary<string, double> _at =
        parameters.ToDictionary(one => one.Key, one => one.Default);

    /// <inheritdoc/>
    public double Get(string key) => _at.TryGetValue(key, out double value) ? value : 0;

    /// <inheritdoc/>
    public void Set(string key, double value) => _at[key] = value;
}

/// <summary>
/// The application the panel is drawn under, which is the theme and nothing else.
/// </summary>
/// <remarks>
/// Not the real one. That opens the main window, the audio device and the MIDI ports the moment
/// it is set up, none of which a picture of a panel needs and any of which can fail on a machine
/// with no sound card. The device's own colours are painted on afterwards by the same
/// <see cref="PanelTint"/> the application paints them with.
/// </remarks>
internal sealed class Shot : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }
}

/// <summary>
/// Draws one machine's or effect's panel to a PNG, with no window and no screen.
/// </summary>
/// <remarks>
/// A described panel is laid out by what its file says, and what a file says and what it comes
/// out looking like are far enough apart that a control can sit in the wrong place, or on top of
/// another one, with nothing in the file looking wrong. Before this the only way to see a panel
/// was to start the application, load a song, open the device and photograph the screen, which is
/// slow enough that a layout gets changed more often than it gets looked at.
///
/// It draws the real thing: the same <see cref="PanelView"/> the application draws, the same
/// readers off disc, and the same theme. What it cannot show is anything that is a fact about a
/// live device rather than about the panel, such as the wave in a scope or the picture of a take,
/// since there is no engine behind it. Those come out as the empty boxes they are before anything
/// is loaded.
///
/// Usage: <c>dotnet run -- &lt;device folder&gt; &lt;out.png&gt; [width]</c>. With no width the
/// window is as wide as the panel wants to be, which is how the application sizes it and so the
/// honest answer to how much room a layout takes. A width forces one, for seeing how a panel sits
/// in a space of a known size.
/// </remarks>
internal static class Program
{
    /// <summary>The widest the application lets one of these windows be.</summary>
    private const double MostWidth = 1400;

    /// <summary>The room around the panel, which is the plate the application draws it on.</summary>
    private const double Around = 12;

    /// <summary>The key the theme's page colour is put up under, which the plate is painted in.</summary>
    private const string BackgroundKey = "Color.Background";

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("panelshot <device folder> <out.png> [width]");

            return 2;
        }

        string folder = args[0];
        string into = args[1];
        double width = args.Length > 2 ? double.Parse(args[2]) : 0;

        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine(folder + " is not there");

            return 2;
        }

        AppBuilder.Configure<Shot>()
                  .UseSkia()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                  .WithInterFont()
                  .SetupWithoutStarting();

        var (face, theme) = Read(folder);

        if (face is null)
        {
            Console.Error.WriteLine(folder + " holds no machine and no effect");

            return 1;
        }

        var plate = new Border
        {
            Padding = new Thickness(Around),
            Child = new PanelView { Face = face, Values = new Standing(face.Parameters) },
        };

        var window = new Window
        {
            SizeToContent = width > 0 ? SizeToContent.Height : SizeToContent.WidthAndHeight,
            MaxWidth = MostWidth,
            Content = plate,
        };

        if (width > 0) window.Width = width;

        new PanelTint().Apply(plate, theme);

        if (plate.TryFindResource(BackgroundKey, out object? behind) && behind is Color colour)
            window.Background = new SolidColorBrush(colour);

        window.Show();

        if (window.CaptureRenderedFrame() is not { } frame)
        {
            Console.Error.WriteLine("nothing rendered");

            return 1;
        }

        frame.Save(into);

        Console.WriteLine(into + " " + frame.PixelSize.Width + "x" + frame.PixelSize.Height);

        return 0;
    }

    /// <summary>
    /// The face and the colours of whichever kind of device is in that folder.
    /// </summary>
    /// <remarks>
    /// Told apart by the file, since that is all the application has to go on as well: a machine's
    /// folder holds a machine.json and an effect's holds an effect.json.
    /// </remarks>
    private static (Face? Face, Rack.SoundDevices.Faces.Records.PanelTheme? Theme) Read(string folder)
    {
        if (File.Exists(Path.Combine(folder, "machine.json")))
        {
            return SoundMachineProject.Open(folder) is { } machine
                ? (new Face(machine.Panel, machine.Parameters, folder), machine.Theme)
                : (null, null);
        }

        return SoundEffectProject.Open(folder) is { } effect
            ? (new Face(effect.Panel, effect.Parameters, folder), effect.Theme)
            : (null, null);
    }
}
