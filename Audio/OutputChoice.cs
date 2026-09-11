using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Two comparisons and the first one wins, which is the whole of it. Nothing is read and nothing
/// is opened, so this can be put a question to without a sound card.
/// </remarks>
public sealed class OutputChoice : IOutputChoice
{
    /// <inheritdoc/>
    public AudioOutput? Among(IReadOnlyList<AudioOutput>? offered, string? name, int id)
    {
        if (offered == null || offered.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(name))
        {
            foreach (var one in offered)
            {
                if (Named(one, name)) return one;
            }
        }

        if (id < 0) return null;

        foreach (var one in offered)
        {
            if (one != null && one.Id == id) return one;
        }

        return null;
    }

    /// <summary>Whether that output is the one with this name.</summary>
    /// <remarks>
    /// The plain name rather than what a picker draws, since the drawn one has the word ASIO put
    /// on the end of it and what was stored is the device's own.
    /// </remarks>
    /// <param name="output">One of the offered outputs.</param>
    /// <param name="name">What the chosen one was called.</param>
    private static bool Named(AudioOutput? output, string name) =>
        output != null
        && string.Equals(output.Name?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);
}
