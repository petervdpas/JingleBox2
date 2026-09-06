using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class MmDeviceToken : IMmDeviceToken
{
    /// <summary>What a device interface path for one of these begins with.</summary>
    /// <remarks>
    /// Written out rather than assembled, so the literal a reader would search for is in the
    /// source. It is the system's own spelling and none of it is ours to choose.
    /// </remarks>
    private const string Prefix = @"\\?\SWD#MMDEVAPI#";

    /// <summary>And the class it ends with, which says the endpoint plays rather than records.</summary>
    private const string Render = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    /// <inheritdoc/>
    public string Wrap(string? endpoint) =>
        string.IsNullOrWhiteSpace(endpoint) ? "" : Prefix + endpoint + Render;

    /// <inheritdoc/>
    public string Unwrap(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "";

        string plain = token;

        if (plain.StartsWith(Prefix, StringComparison.Ordinal)) plain = plain[Prefix.Length..];
        if (plain.EndsWith(Render, StringComparison.Ordinal)) plain = plain[..^Render.Length];

        return plain;
    }
}
