using System;
using System.Windows.Input;

namespace JingleBox2.Rack.Controls;

/// <summary>
/// A wheel was moved, wrapped as a command because that is what the wheel takes.
/// </summary>
/// <remarks>
/// <see cref="Struck"/> for a key and this for a wheel, and both exist for the same reason:
/// <see cref="Wheel"/> hands its new position back through an <see cref="ICommand"/>, since it
/// was written for a panel put together in XAML where a command is what there is to bind, and a
/// panel drawn from a description has no bindings and only wants to be told.
///
/// Always ready. There is no state in which a wheel cannot be moved: a machine that does nothing
/// with a modulation wheel is a machine that does nothing with it, which is not the same as a
/// control that has been greyed out.
/// </remarks>
internal sealed class Turned(Action<double> moved) : ICommand
{
    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc/>
    /// <remarks>
    /// The parameter is where the wheel now is: minus one to one for a pitch wheel and nought to
    /// one for a modulation wheel. Anything else is ignored rather than throwing, for the reason
    /// <see cref="Struck.Execute"/> gives.
    /// </remarks>
    public void Execute(object? parameter)
    {
        if (parameter is double where) moved(where);
    }

    /// <summary>Never raised: this can always be run.</summary>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
