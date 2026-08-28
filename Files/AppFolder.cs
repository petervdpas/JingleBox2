using System;
using JingleBox2.Files.Interfaces;

namespace JingleBox2.Files;

/// <inheritdoc/>
public sealed class AppFolder : IAppFolder
{
    /// <inheritdoc cref="IAppFolder.Name"/>
    public const string AppName = "JingleBox2";

    /// <inheritdoc/>
    string IAppFolder.Name => AppName;

    /// <inheritdoc/>
    public string Path(string appName) =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);

    /// <inheritdoc/>
    public string Path() => Path(AppName);
}
