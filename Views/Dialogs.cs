using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
public sealed class Dialogs : IDialogs
{
    /// <inheritdoc/>
    public Window? Owner
    {
        get
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            Window? front = null;

            foreach (var window in desktop.Windows)
                if (window.IsActive) front = window;

            return front ?? desktop.MainWindow;
        }
    }

    /// <inheritdoc/>
    public Task<T> ShowAsync<T>(Window dialog, T whenNone)
    {
        var owner = Owner;

        return owner == null ? Task.FromResult(whenNone) : dialog.ShowDialog<T>(owner);
    }
}
