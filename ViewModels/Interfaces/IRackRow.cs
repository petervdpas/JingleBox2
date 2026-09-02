using Avalonia.Media;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// One row on the rack, whichever of the two tabs it is on.
/// </summary>
/// <remarks>
/// A machine's box and an effect's are drawn by one template and washed by one set of styles,
/// because to the eye they are the same thing: a coloured box with a name and a line under it.
/// What differs is everything behind the row, which is why there are two classes rather than one
/// with a flag: a machine's box is an instrument on the shelf with settings of its own, and an
/// effect's is the effect itself, since an effect in use is a slot on a chain rather than
/// something kept here.
///
/// It is named in XAML, which is the reason it exists as a contract rather than as a shared base
/// class: a compiled binding needs a type, one template cannot have two, and a row that had to
/// be drawn twice would be two templates drifting apart.
/// </remarks>
public interface IRackRow
{
    /// <summary>What it is called.</summary>
    string Name { get; }

    /// <summary>The line under the name, in the box's own words.</summary>
    string DetailText { get; }

    /// <summary>Its colour on its own, for the bar down the side of the row.</summary>
    string Colour { get; }

    /// <summary>The row's own wash: the box's colour at the weight its theme asks for.</summary>
    IBrush Row { get; }

    /// <summary>The same colour, heavier, for the row under the pointer.</summary>
    IBrush RowOver { get; }

    /// <summary>And heavier again for the row that is picked.</summary>
    IBrush RowPicked { get; }
}
