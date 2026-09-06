using System;
using System.Linq;
using JingleBox2.Config.Interfaces;
using JingleBox2.Config.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.Config;

/// <inheritdoc/>
/// <remarks>
/// Over the settings file, which is where everything that is about this installation rather than
/// about a song already lives. Written as the hand lets go, since that is once per block moved
/// and the file is small.
/// </remarks>
public sealed class PatchPlaces : IPatchPlaces
{
    /// <summary>The settings, holding the places among everything else.</summary>
    private readonly AppConfig _cfg;

    /// <summary>What writes them out.</summary>
    private readonly IConfigStore _store;

    /// <summary>Takes the settings to keep the places in, and what writes them.</summary>
    /// <param name="cfg">The settings this installation is running on.</param>
    /// <param name="store">What puts them on the disc.</param>
    public PatchPlaces(AppConfig cfg, IConfigStore store)
    {
        _cfg = cfg;
        _store = store;
    }

    /// <inheritdoc/>
    public bool Placed(string node, out double x, out double y)
    {
        x = 0;
        y = 0;

        if (string.IsNullOrEmpty(node)) return false;

        var place = _cfg.PatchbayPlaces.FirstOrDefault(
            p => string.Equals(p.Node, node, StringComparison.Ordinal));

        if (place == null) return false;
        if (!Real(place.X) || !Real(place.Y)) return false;

        x = place.X;
        y = place.Y;

        return true;
    }

    /// <inheritdoc/>
    public void Place(string node, double x, double y)
    {
        if (string.IsNullOrEmpty(node)) return;
        if (!Real(x) || !Real(y)) return;

        var place = _cfg.PatchbayPlaces.FirstOrDefault(
            p => string.Equals(p.Node, node, StringComparison.Ordinal));

        if (place == null)
        {
            place = new PatchbayPlace { Node = node };
            _cfg.PatchbayPlaces.Add(place);
        }

        if (Math.Abs(place.X - x) < 0.5 && Math.Abs(place.Y - y) < 0.5) return;

        place.X = x;
        place.Y = y;

        _store.Save(_cfg);
    }

    /// <summary>Whether a number is one a block can actually be drawn at.</summary>
    /// <remarks>
    /// A settings file is a file, and one edited by hand or written by a version that went wrong
    /// can hold anything. What a place has to survive is being read back and drawn at.
    /// </remarks>
    private static bool Real(double number) => !double.IsNaN(number) && !double.IsInfinity(number);
}
