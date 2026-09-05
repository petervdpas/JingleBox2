using System;
using System.IO;
using JingleBox2.Files.Interfaces;

namespace JingleBox2.Files;

/// <inheritdoc/>
public sealed class PortablePath(IFilePaths? paths = null, IAppFolder? folder = null) : IPortablePath
{
    /// <summary>How this system decides a path is under the application folder.</summary>
    private readonly IFilePaths _paths = paths ?? new FilePaths();

    /// <summary>Where the application keeps its things, which is what a packed path stands in for.</summary>
    private readonly IAppFolder _folder = folder ?? new AppFolder();

    /// <summary>
    /// What stands in for the application folder. Forward slash, on every platform.
    /// </summary>
    /// <remarks>
    /// A separator has to be chosen and written down, because something saved on Windows has to
    /// open on Linux. Forward slash, since that is the one both understand and the one a zip
    /// entry already uses.
    /// </remarks>
    public const string Token = "{app}/";

    /// <inheritdoc/>
    public string Pack(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        string root = _folder.Path();

        if (path.Length <= root.Length + 1) return path;
        if (!path.AsSpan(0, root.Length).Equals(root, _paths.Comparison)) return path;
        if (path[root.Length] != Path.DirectorySeparatorChar && path[root.Length] != '/') return path;

        return Token + path.Substring(root.Length + 1).Replace('\\', '/');
    }

    /// <inheritdoc/>
    public string Unpack(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(Token, StringComparison.Ordinal))
            return path ?? "";

        string rest = path.Substring(Token.Length).Replace('/', Path.DirectorySeparatorChar);

        return Path.Combine(_folder.Path(), rest);
    }
}
