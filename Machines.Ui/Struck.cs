using System;
using System.Windows.Input;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A key was hit, wrapped as a command because that is what the keyboard takes.
/// </summary>
/// <remarks>
/// <see cref="Clavier"/> hands the pressed key back through an <see cref="ICommand"/>, since it
/// was written for a panel put together in XAML where a command is what there is to bind. A
/// panel drawn from a description has no bindings and only wants to be told, so this is the one
/// line between the two.
///
/// Always ready. There is no state in which a key on a machine cannot be pressed: a machine with
/// nothing on it plays nothing, which is not the same as a keyboard that has been greyed out.
/// </remarks>
internal sealed class Struck(Action<int> hit) : ICommand
{
    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (parameter is int semitone) hit(semitone);
    }

    /// <summary>Never raised: this can always be run.</summary>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
