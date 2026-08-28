using System.Collections.Generic;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Which themes there are, what file each one is, and which room each one is for.
/// </summary>
/// <remarks>
/// The whole of what deciding a theme involves, kept apart from putting one on. Putting one on
/// reaches the toolkit and can only happen once, in an application that is up; deciding which
/// one is arithmetic on a name out of a settings file, and that is the half that goes wrong
/// quietly. A name saved before the coloured themes gained a light half has to be followed to
/// what it is called now, and a name nobody follows becomes the default: somebody's chosen
/// theme silently reverts and nothing anywhere says why.
///
/// The set is closed. A theme exists because it is named here, not because a file happens to be
/// sitting beside the others, so a renamed or deleted sheet is an edit a reader can see rather
/// than a resource that quietly fails to load.
/// </remarks>
public interface IThemeCatalogue
{
    /// <summary>The base sheet every theme is layered on top of.</summary>
    /// <remarks>
    /// Two sheets are merged, always: this one and the theme itself. So a theme file says only
    /// what it changes, and a rule that is the same everywhere is written down once.
    /// </remarks>
    string BaseSheet { get; }

    /// <summary>What is worn when the settings name nothing, or name something we have not got.</summary>
    string Default { get; }

    /// <summary>
    /// The themes there are, in picker order.
    /// </summary>
    /// <remarks>
    /// In pairs, dark then light, because that is how somebody looks for one: they know the
    /// colours they want and then which room they are sitting in. The plain pair comes first
    /// and is called nothing else, having no colour to be named after.
    /// </remarks>
    IReadOnlyList<string> Names { get; }

    /// <summary>
    /// The name this one really is, or <see cref="Default"/> when it is nothing we have.
    /// </summary>
    /// <param name="themeName">What the settings say, or null.</param>
    string Resolve(string? themeName);

    /// <summary>The file a theme is, or the default's file when it is nothing we have.</summary>
    /// <param name="themeName">What the settings say, or null.</param>
    string SheetFor(string? themeName);

    /// <summary>
    /// Whether a theme's room is a lit one.
    /// </summary>
    /// <remarks>
    /// Written down per theme rather than worked out from the background colour, so adding a
    /// theme makes somebody say which kind it is. It is not decoration: Fluent draws its own
    /// parts from the variant, the popups, the scrollbars and the text boxes, and a pale sheet
    /// under the dark variant gets dark popups over a white page.
    /// </remarks>
    /// <param name="themeName">What the settings say, or null.</param>
    bool IsLit(string? themeName);
}
