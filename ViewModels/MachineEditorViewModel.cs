using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Machines;
using JingleBox2.Tracker.Machines;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace JingleBox2.ViewModels;

/// <summary>
/// The machine editor: one project open at a time, and what can be done with it.
/// </summary>
/// <remarks>
/// The other half of the rack. The rack holds machines that are registered and ready for a song
/// to take an instrument off; this is where one is made in the first place. New, open, save, and
/// install it so the rack has it.
///
/// Nothing about a song here, and nothing about instruments: a machine is a box, and it becomes
/// an instrument only when a song takes it.
/// </remarks>
public sealed partial class MachineEditorViewModel : ObservableObject
{
    /// <summary>The project being worked on, or null when nothing is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(Title))]
    private MachineProject? project;

    public bool HasProject => Project != null;

    /// <summary>What the editor says it is showing.</summary>
    public string Title => Project == null
        ? "No machine open"
        : Project.Name.Length > 0 ? Project.Name : "Untitled machine";

    [ObservableProperty] private string status = "";

    /// <summary>
    /// A machine nobody has saved yet, with an id of its own from the start.
    /// </summary>
    /// <remarks>
    /// The id is made once and never changes, because it is what every song that uses this
    /// machine writes down. Made from the name it is given later would mean a machine losing
    /// its songs the first time it is renamed.
    /// </remarks>
    public IRelayCommand NewCommand => new RelayCommand(() =>
    {
        Project = new MachineProject
        {
            Id = "machine." + Guid.NewGuid().ToString("n")[..8],
            Name = "New machine",
            Version = "1.0",
            Theme = new MachineTheme("#7B838C")
        };

        Status = "A new machine. Save it somewhere to start keeping it.";
    });

    /// <summary>Opens the project in that folder.</summary>
    public void Open(string folder)
    {
        var opened = MachineProject.Open(folder);

        if (opened == null)
        {
            Status = "No machine in " + folder;
            return;
        }

        Project = opened;
        Status = "Opened '" + opened.Name + "'";
    }

    /// <summary>Writes it where it lives, or where it is being put for the first time.</summary>
    public void Save(string? folder = null)
    {
        if (Project == null) return;

        try
        {
            Project.Save(folder);
            Status = "Saved to " + Project.Folder;

            OnPropertyChanged(nameof(Title));
        }
        catch (Exception ex)
        {
            Status = "Could not save: " + ex.Message;
        }
    }

    /// <summary>True when saving needs somewhere to save to.</summary>
    public bool NeedsFolder => Project is { IsSaved: false };

    /// <summary>
    /// Adds a parameter, named so it can be told from the others and no more.
    /// </summary>
    /// <remarks>
    /// A machine is its parameters, so this is the ordinary way to build one: add, name, set
    /// the range, and the panel is drawn from what is here.
    /// </remarks>
    public IRelayCommand AddParameterCommand => new RelayCommand(() =>
    {
        if (Project == null) return;

        int at = Project.Parameters.Count + 1;

        var parameter = new MachineParameter
        {
            Key = "p" + at,
            Name = "Parameter " + at,
            Min = 0,
            Max = 1,
            Default = 0,
            Step = 0.01
        };

        Project.Parameters.Add(parameter);
        Parameters.Add(new MachineParameterViewModel(parameter));

        Status = "Added a parameter. Save when you are happy with it.";
    });

    /// <summary>Takes one out. What a song saved under that key is simply not read again.</summary>
    public IRelayCommand<MachineParameterViewModel> RemoveParameterCommand =>
        new RelayCommand<MachineParameterViewModel>(parameter =>
        {
            if (Project == null || parameter == null) return;

            Project.Parameters.Remove(parameter.Parameter);
            Parameters.Remove(parameter);

            Status = "Removed '" + parameter.Name + "'";
        });

    /// <summary>
    /// The parameters of the machine that is open.
    /// </summary>
    /// <remarks>
    /// A collection of its own rather than the project's list handed out: a list that is added
    /// to is still the same list, so a page bound straight to it is never told anything has
    /// changed. This is filled from the project when one is opened and kept in step with it.
    /// </remarks>
    public ObservableCollection<MachineParameterViewModel> Parameters { get; } = new();

    partial void OnProjectChanged(MachineProject? value)
    {
        Parameters.Clear();

        if (value == null) return;

        foreach (var parameter in value.Parameters) Parameters.Add(new MachineParameterViewModel(parameter));
    }
}
