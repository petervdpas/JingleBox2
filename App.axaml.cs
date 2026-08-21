using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using JingleBox2.UI;

namespace JingleBox2;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        ThemeManager.Apply("Dark");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Plugins with interfaces of their own have to be called on the thread their windows
        // live on. Until this is said, the run loop pumps on a thread of its own, which is
        // right for effects with no window and wrong the moment one has.
        JingleBox2.Audio.Plugins.Vst3RunLoop.DriveWith(round => Dispatcher.UIThread.Post(round));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://JingleBox2/Assets/icon.ico")))
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}