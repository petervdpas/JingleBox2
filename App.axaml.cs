using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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