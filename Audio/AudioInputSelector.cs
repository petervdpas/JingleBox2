using System.Collections.Generic;
using System.Linq;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class AudioInputSelector : IAudioInputSelector
{
    /// <inheritdoc cref="IAudioInputSelector.Fallback"/>
    public const string Default = "Default";

    /// <inheritdoc/>
    string IAudioInputSelector.Fallback => Default;

    /// <inheritdoc/>
    public string Pick(IEnumerable<string> devices, string? preferred)
    {
        if (devices is null) return Default;

        var list = devices as IList<string> ?? devices.ToList();

        if (!string.IsNullOrEmpty(preferred) && list.Contains(preferred))
            return preferred;

        return list.FirstOrDefault() ?? Default;
    }
}
