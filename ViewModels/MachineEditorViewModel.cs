using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Machines;
using JingleBox2.Tracker.Machines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.IO;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Machines.Records;
using JingleBox2.Tracker.Machines.Interfaces;
using JingleBox2.Views.Interfaces;
using JingleBox2.Views;

namespace JingleBox2.ViewModels;

/// <summary>
/// The machine editor: one project open at a time, and what can be done with it.
/// </summary>
/// <remarks>
/// The other half of the rack. The rack holds machines that are registered and ready for a song
/// to take an instrument off; this is where one is made in the first place. New, open, save, and
/// export it as a zip for somebody else to import.
///
/// Nothing about a song here, and nothing about instruments: a machine is a box, and it becomes
/// an instrument only when a song takes it.
/// </remarks>
public sealed partial class MachineEditorViewModel : ObservableObject
{
    /// <summary>What a key is called, both ways round.</summary>
    private readonly IMachineNotes _notes = new MachineNotes();

    /// <summary>A machine's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IMachineTint _tint = new MachineTint();

    /// <summary>A machine going into a zip and coming back out.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMachineArchive Crates = new MachineArchive();

    /// <summary>
    /// What has been done to the machine being designed, so it can be taken back.
    /// </summary>
    /// <remarks>
    /// The designer's own. Undo belongs to the thing being edited: a step here is the machine as
    /// its file would hold it, and a step in the tracker is a pattern's cells, and one history
    /// pretending they were the same kind of thing would be a lie about both.
    /// </remarks>
    public DesignHistory History { get; } = new();

    /// <summary>True while the machine on screen is not the machine in the folder.</summary>
    /// <remarks>
    /// Shown by the Save button warming, the same as the song's. A machine that has never been
    /// saved counts as having changes from its first edit, which is right: there is nothing in
    /// any folder that matches it.
    /// </remarks>
    public bool NeedsSaving => Project != null && History.NeedsSaving;

    /// <summary>
    /// True when there is a saved copy to go back to and something to go back from.
    /// </summary>
    /// <remarks>
    /// A machine that has never been written down has nothing to return to. Its changes are the
    /// whole of it, and the way to be rid of those is New.
    /// </remarks>
    public bool CanCancelChanges => Project is { IsSaved: true } && History.NeedsSaving;

    /// <summary>Throws away everything since the last save. Asked first by the view.</summary>
    public bool CancelChanges()
    {
        if (!CanCancelChanges) return false;

        if (!History.Cancel(Project)) return false;

        Rewrap();

        Status = "Back to " + Project!.Name + " as it was last saved.";

        return true;
    }

    /// <summary>The project being worked on, or null when nothing is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(CanDesign))]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(Folder))]
    [NotifyPropertyChangedFor(nameof(Accent))]
    [NotifyPropertyChangedFor(nameof(AccentHex))]
    private MachineProject? project;

    /// <summary>
    /// Builds the editor with nothing open, and wires the three pages together.
    /// </summary>
    /// <remarks>
    /// The tools tab comes and goes with the tools, and the tab strip is what moves off a page
    /// that has gone. Doing that here as well put the strip and this in a race: opening the page
    /// reads the machines again, the list is empty for the instant it takes to rebuild, and a page
    /// that closed itself on the strength of that instant snapped straight back to the panel every
    /// time it was opened. So this only says the answer moved and lets the strip decide.
    /// </remarks>
    public MachineEditorViewModel()
    {
        History.Changed += HistoryMoved;

        Values = new MachinePreviewValues(Parameters);

        PresetDesk = new MachinePresetDesk(() => Project);

        Utilities = new MachineUtilities(() => Project);

        Utilities.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MachineUtilities.HasWork)) OnPropertyChanged(nameof(ShowsUtilities));
        };

        PreviewMenu = new Midi.MachineLinks(
            () => Project?.Id ?? "",
            () => Project?.Name ?? "")
        {
            Told = said => Status = said
        };
    }

    /// <summary>
    /// The presets this machine ships with, edited as the files they are.
    /// </summary>
    /// <remarks>
    /// Its own page beside the panel, because it is its own job. Laying out a face and deciding
    /// what the machine sounds like when somebody first meets it have nothing to say to each
    /// other, and putting them on one screen means neither gets the room.
    /// </remarks>
    public MachinePresetDesk PresetDesk { get; }

    /// <summary>
    /// The jobs that are neither drawing a panel nor filling in a preset.
    /// </summary>
    /// <remarks>
    /// Its own page for the same reason the presets have one: renaming a kit and levelling its
    /// recordings have nothing to say to each other about layout, and neither of them is
    /// something you do while you are doing anything else.
    /// </remarks>
    public MachineUtilities Utilities { get; } = null!;

    /// <summary>
    /// Which page is open: nought the screen, one the presets, two the tools.
    /// </summary>
    /// <remarks>
    /// A number rather than two flags, since exactly one is open and two flags can say
    /// otherwise.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnScreen))]
    [NotifyPropertyChangedFor(nameof(OnPresets))]
    [NotifyPropertyChangedFor(nameof(OnUtilities))]
    [NotifyPropertyChangedFor(nameof(CanDesign))]
    private int page;

    /// <summary>True on the page where the panel is laid out.</summary>
    /// <remarks>
    /// What the Design switch hangs off. Turning the panel's design mode on and off from the
    /// presets page would be a switch about something nobody can see.
    /// </remarks>
    public bool OnScreen => Page == 0;

    /// <summary>True when the Design switch has anything to be about.</summary>
    /// <remarks>
    /// Both halves, because the switch is meaningless twice over otherwise: on the presets page
    /// there is no panel on screen to design, and with no machine open there is no panel at all.
    /// </remarks>
    public bool CanDesign => HasProject && OnScreen;

    /// <summary>True on the page where the presets are filled in.</summary>
    public bool OnPresets => Page == 1;

    /// <summary>True on the page holding the jobs that are neither of those.</summary>
    public bool OnUtilities => Page == 2;

    /// <summary>
    /// True while the tools have anything to work on, which is a machine with a preset in it.
    /// </summary>
    /// <remarks>
    /// Renaming a preset is nearly always something somebody wants, so this is nearly always
    /// true; the case it covers is an installation with the rack emptied, where the page would
    /// be two cards about nothing.
    /// </remarks>
    public bool ShowsUtilities => Utilities.HasWork;

    /// <summary>
    /// Reads the machine's presets folder when that tab is opened.
    /// </summary>
    /// <remarks>
    /// On being opened rather than on every change, because the folder is a folder: somebody may
    /// have put a file in it from outside, and asking the disc on the way in is the only moment
    /// that could be noticed without watching it.
    ///
    /// The tools are read again for the same reason one step further along: a level is a fact
    /// about a file, and the file may have been rewritten by anything since the last time this
    /// page was looked at.
    /// </remarks>
    partial void OnPageChanged(int value)
    {
        if (OnPresets) PresetDesk.Reread();

        if (OnUtilities) Utilities.Reread();
    }

    /// <summary>True when a machine is open, which is what most of this page hangs on.</summary>
    public bool HasProject => Project != null;

    /// <summary>What the editor says it is showing.</summary>
    public string Title => Project == null
        ? "No machine open"
        : Project.Name.Length > 0 ? Project.Name : "Untitled machine";

    /// <summary>The line along the bottom: what the last thing done here did, or why it did not.</summary>
    [ObservableProperty] private string status = "";

    /// <summary>
    /// Where the open machine keeps its own files, or nothing when it has never been saved.
    /// </summary>
    /// <remarks>
    /// The panel needs it, because a picture on a machine's face is named against the machine's
    /// folder and nothing else. Said here rather than read off the project, so that a machine
    /// saved for the first time announces the folder it has just been given: the project is
    /// plain data and says nothing when its folder is written.
    /// </remarks>
    public string Folder => Project?.Folder ?? "";

    /// <summary>
    /// The colour the machine is, as the picker deals in it.
    /// </summary>
    /// <remarks>
    /// On the page rather than behind the dialog with the other seven, because it is the one of
    /// the eight that every machine has an opinion about: the colour is how you know which
    /// machine you are in front of, and the seven are how deep its face is.
    /// </remarks>
    public Color Accent
    {
        get => _tint.Hue(Project?.Theme.Accent, out var hue) ? hue : Colors.Gray;
        set => Wear(_tint.Hex(value));
    }

    /// <summary>The same colour written down, for somebody who has the number already.</summary>
    /// <remarks>
    /// Anything that is not a colour is refused and the box put back to what it was showing.
    /// Half a number typed into a box is not a machine wearing half a colour.
    /// </remarks>
    public string AccentHex
    {
        get => Project?.Theme.Accent ?? "";
        set
        {
            if (!_tint.Hue((value ?? "").Trim(), out var hue))
            {
                Status = "'" + value + "' is not a colour, so nothing changed.";

                OnPropertyChanged();

                return;
            }

            Wear(_tint.Hex(hue));

            OnPropertyChanged();
        }
    }

    /// <summary>The grey a machine wears until it is given a colour of its own.</summary>
    private const string Bare = "#7B838C";

    /// <summary>Paints the machine that colour, keeping the seven distances it already had.</summary>
    private void Wear(string colour)
    {
        if (Project is not { } project) return;

        if (string.Equals(project.Theme.Accent, colour, StringComparison.OrdinalIgnoreCase)) return;

        Dressed(project.Theme with { Accent = colour });
    }

    /// <summary>
    /// Puts a whole theme on the machine, which is what comes back from the colours dialog.
    /// </summary>
    /// <remarks>
    /// Said out loud even where nothing on this page shows it, because the panel beside it is
    /// painted from the theme by the view and has no other way of hearing that it moved.
    ///
    /// The colour is part of the machine, so changing it is a change to the machine: the Save
    /// button has to warm and it has to be something undo can take back. This used to say what it
    /// did to the two things showing the colour and to nothing else.
    /// </remarks>
    public void Dressed(MachineTheme theme)
    {
        if (Project is not { } project) return;

        project.Theme = theme;

        OnPropertyChanged(nameof(Accent));
        OnPropertyChanged(nameof(AccentHex));

        Redraw();
    }

    /// <summary>The colours as they stand, for the dialog that fine tunes them.</summary>
    public MachineTheme Theme => Project?.Theme ?? new MachineTheme(Bare);

    /// <summary>
    /// A machine nobody has saved yet, with an id of its own from the start.
    /// </summary>
    /// <remarks>
    /// The id is made once and never changes, because it is what every song that uses this
    /// machine writes down. Made from the name it is given later would mean a machine losing
    /// its songs the first time it is renamed.
    ///
    /// Always enabled. Whatever is open is simply let go of, and what was in a folder is still in
    /// that folder.
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
    /// <remarks>
    /// The presets are read before the panel is told, or the picker on it is handed a list that has
    /// not been filled in yet and draws an empty one.
    /// </remarks>
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

        PresetDesk.Reread();

        ShelfChanged();
    }

    /// <summary>
    /// Writes it where it lives, or where it is being put for the first time.
    /// </summary>
    /// <remarks>
    /// What is written down and what is in the folder have to be the same machine, so the pictures
    /// are put in order first: anything nothing names is deleted, the numbers close up, and the
    /// panel is pointed at the names the files now have.
    ///
    /// A saved machine is a machine that changed, so the number that says which one it is goes up
    /// by itself. Left to a hand it is bumped when somebody remembers, which is to say it is
    /// bumped after five changes and not after the sixth. Unless the number was typed: somebody
    /// who has just written 2.0 in the box means 2.0, and adding a hundredth to it on the way out
    /// would make the box unusable.
    ///
    /// The version is on a plain object the box is bound straight through to, so the whole project
    /// is said again for the box to show what was just written.
    ///
    /// A machine saved for the first time has somewhere to keep presets for the first time, which
    /// is the moment that page stops being empty.
    /// </remarks>
    public void Save(string? folder = null) => Write(folder, carry: false);

    /// <summary>
    /// Writes the machine into another folder, and works there from now on.
    /// </summary>
    /// <remarks>
    /// The whole machine, not the manifest alone. A machine is its folder: the manifest names
    /// pictures, presets and sounds by the names they have inside it, so writing it into an
    /// empty folder somewhere else would leave a machine that draws nothing. The files go first
    /// and the manifest after them, through <see cref="IMachineArchive.CopyInto"/>.
    ///
    /// Save as, in the ordinary sense: the editor is pointed at the new folder afterwards and
    /// the old one is left exactly as it was. That is what makes it the way to put an edited
    /// machine back over the copy that ships beside the program, which is the case it was
    /// written for.
    ///
    /// The id does not change, deliberately. A machine's id is what songs write down and what
    /// decides which engine it runs on, so a copy of a machine is that machine somewhere else,
    /// not a new one. Making a new machine is New.
    /// </remarks>
    /// <param name="folder">Where the machine is to live from now on.</param>
    public void SaveAs(string folder) => Write(folder, carry: true);

    /// <summary>Both of the above, since only one line differs between them.</summary>
    /// <param name="folder">Where to write, or null for wherever the machine already lives.</param>
    /// <param name="carry">Whether the folder's other files go too, which is what Save as means.</param>
    private void Write(string? folder, bool carry)
    {
        if (Project == null) return;

        try
        {
            Tidy();

            if (string.Equals(Project.Version, _versionWritten, StringComparison.Ordinal))
                Project.Version = Bumped(Project.Version);

            _versionWritten = Project.Version;

            if (carry && Project.IsSaved) Crates.CopyInto(Project, folder!);

            Project.Save(folder);
            Status = "Saved " + Project.Version + " to " + Project.Folder;

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Folder));

            OnPropertyChanged(nameof(Project));

            History.Saved(Project);

            PresetDesk.Reread();
        }
        catch (Exception ex)
        {
            Status = "Could not save: " + ex.Message;
        }
    }

    /// <summary>True when saving needs somewhere to save to.</summary>
    public bool NeedsFolder => Project is { IsSaved: false };

    /// <summary>True once there is something on disc worth handing to somebody.</summary>
    public bool CanExport => Project is { IsSaved: true };

    /// <summary>What to call the zip, before anybody has said where to put it.</summary>
    public string ExportName => Project == null ? "machine.zip" : Safe(Project.Name, "machine") + ".zip";

    /// <summary>
    /// Writes the machine out as a zip.
    /// </summary>
    /// <remarks>
    /// This is how a machine leaves here. Not installing: what the app runs is read from its own
    /// machines folder, and getting a machine in there is the importer's job on the SETTINGS
    /// tab. Keeping the two apart means a machine you were handed and a machine you made arrive
    /// by exactly the same road, which is the only way to find out that the road works.
    ///
    /// The project stays where you keep your work. The zip is a copy of it, and the folder it
    /// came from is nobody else's business, so nothing about the path goes into the file.
    /// </remarks>
    public void Export(string zipPath)
    {
        if (Project is not { IsSaved: true } project) return;

        try
        {
            Crates.Export(project, zipPath);

            Status = "Exported '" + project.Name + "' to " + zipPath;
        }
        catch (Exception ex)
        {
            Status = "Could not export: " + ex.Message;
        }
    }

    /// <summary>A file name that will not fight the file system, from the machine's own name.</summary>
    private static string Safe(string name, string fallback)
    {
        string wanted = (name ?? "").Trim();

        foreach (char bad in Path.GetInvalidFileNameChars()) wanted = wanted.Replace(bad, ' ');

        wanted = wanted.Trim();

        return wanted.Length > 0 ? wanted : fallback;
    }

    /// <summary>
    /// Adds a parameter, named so it can be told from the others and no more.
    /// </summary>
    /// <remarks>
    /// A machine is its parameters, so this is the ordinary way to build one: add, name, set
    /// the range, and the panel is drawn from what is here.
    ///
    /// Always enabled; with nothing open it does nothing.
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

        var wrapped = new MachineParameterViewModel(parameter);

        wrapped.PropertyChanged += Edited;

        Project.Parameters.Add(parameter);
        Parameters.Add(wrapped);

        ParametersChanged();

        Status = "Added a parameter. Save when you are happy with it.";
    });

    /// <summary>Takes one out. What a song saved under that key is simply not read again.</summary>
    /// <remarks>Always enabled; a row exists only while its parameter does.</remarks>
    public IRelayCommand<MachineParameterViewModel> RemoveParameterCommand =>
        new RelayCommand<MachineParameterViewModel>(parameter =>
        {
            if (Project == null || parameter == null) return;

            Project.Parameters.Remove(parameter.Parameter);
            Parameters.Remove(parameter);

            ParametersChanged();

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

    /// <summary>What the panel in the editor reads and writes: the parameters, and nothing kept.</summary>
    public IMachineValues Values { get; }

    /// <summary>
    /// Where a recording's picture comes from while a panel is being laid out.
    /// </summary>
    /// <remarks>
    /// The real shelf, not a pretend one. A machine with a waveform on its face is laid out
    /// against a real recording or it is laid out against nothing, and nothing is the wrong
    /// size: the picture is 340 wide because somebody looked at one.
    ///
    /// Set by whoever builds the editor, since the shelf belongs to the application and the
    /// editor is only borrowing it.
    /// </remarks>
    public IMachineTakes? Takes { get; set; }

    /// <summary>Said when the machine open has changed, since its picker offers a different list.</summary>
    private void ShelfChanged() => OnPropertyChanged(nameof(Presets));

    /// <summary>
    /// A kit for the pads on the panel being laid out, and a recording for the chop control.
    /// </summary>
    /// <remarks>
    /// Made here rather than handed in, unlike the takes and the presets, because neither is
    /// anybody's: the takes are yours and the presets are the machine's, and these two are a
    /// demonstration that exists so the controls have their real shape while a panel is being
    /// laid out around them.
    /// </remarks>
    public IMachinePads PreviewPads { get; } = new MachinePreviewKit();

    /// <inheritdoc cref="PreviewPads"/>
    public IMachineSlices PreviewSlices { get; } = new MachinePreviewSlices();

    /// <summary>And a map for the panel to draw, for the same reason there is a kit.</summary>
    public IMachineZones PreviewZones { get; } = new MachinePreviewMap();

    /// <summary>And a wave, so a machine laid out round a picture is laid out round a picture.</summary>
    public IMachineScope PreviewScope { get; } = new MachinePreviewScope();

    /// <summary>And a pattern to count, so the lamps and their pages take the room they will.</summary>
    public IMachineLocation PreviewLocation { get; } = new MachinePreviewLocation();

    /// <summary>
    /// What the hardware on this desk does to the machine being laid out.
    /// </summary>
    /// <remarks>
    /// The real thing rather than a demonstration, unlike the kit and the map beside it, and it
    /// can be: the machine open here has the id everything else in this application knows it by,
    /// so what your controllers do to it is a real answer and applying a template really applies
    /// one. A made up list would be worse than useless, since somebody laying out a Links part
    /// would be judging its width against names nobody has.
    ///
    /// It says what it did on the line under the title, which is where this page says everything
    /// else it has done.
    /// </remarks>
    public IMachineMenu PreviewMenu { get; }

    /// <summary>
    /// What the picker on the panel being laid out offers.
    /// </summary>
    /// <remarks>
    /// Whatever the machine open right now would offer on the rack, and not always your takes.
    /// A picker laid out against the wrong list is laid out against the wrong width: your shelf
    /// carries a category dropdown in front of it and a machine's own presets do not, so a panel
    /// designed against the wrong one is a panel with a control missing or a control too many.
    ///
    /// The same reason the pictures are real: a picker laid out against five made up names is laid
    /// out against the wrong widths, and the category in front of it cannot be judged at all
    /// without the categories you actually file takes under.
    /// </remarks>
    public IMachinePresets? Presets =>
        Project is { } project && project.BrowsesTakes() != true
            ? new MachinePresetNames(PresetDesk)
            : Shelf;

    /// <summary>Your recordings, handed in by whoever built the editor.</summary>
    public IMachinePresets? Shelf { get; set; }

    /// <summary>
    /// Puts a recording on the panel being designed, wherever the panel keeps one.
    /// </summary>
    /// <remarks>
    /// The machine says which setting holds its take, by naming one on its Take element, so
    /// that is what is asked rather than a name being assumed. A machine with two of them takes
    /// the first, which is the one at the top of the panel and the one somebody laying it out
    /// is looking at.
    ///
    /// Nothing is kept: this is a preview, and what it is showing goes when the editor is
    /// closed. What is being laid out is the panel, not the sound.
    /// </remarks>
    public void PutTake(string path)
    {
        if (Project?.Panel.Root is not { } root) return;

        if (Holder(root) is not { Length: > 0 } key) return;

        Values.SetText(key, path);

        Redraw();
    }

    /// <summary>The setting the machine keeps its recording in, or nothing when it keeps none.</summary>
    private static string? Holder(MachineElement element)
    {
        if (element.Element == MachineElementKinds.Take && element.Parameter.Length > 0)
            return element.Parameter;

        foreach (var child in element.Children)
            if (Holder(child) is { Length: > 0 } found) return found;

        return null;
    }

    /// <summary>
    /// True while the panel is being arranged rather than played with.
    /// </summary>
    /// <remarks>
    /// Designing, a click picks the control up to be worked on; off, the same click turns it.
    /// A designer needs both, because a panel you cannot try is a drawing and a panel you
    /// cannot pick apart is finished.
    /// </remarks>
    [ObservableProperty] private bool designing = true;

    /// <summary>
    /// Whether the parts library is unfolded.
    /// </summary>
    /// <remarks>
    /// Five cards stand round one panel, and the panel is the only one of the six anybody draws
    /// on. So each card folds away to its own title, and a column with nothing open left in it
    /// gives its width back, which is what the folding is for. None of this is written down: the
    /// designer opens the way it always has, with everything showing.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PartsHeight))]
    [NotifyPropertyChangedFor(nameof(LeftWidth))]
    [NotifyPropertyChangedFor(nameof(PartsPanelSizable))]
    [NotifyPropertyChangedFor(nameof(LeftColumnSizable))]
    private bool partsOpen = true;

    /// <summary>Whether the panel, as a list, is unfolded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelHeight))]
    [NotifyPropertyChangedFor(nameof(LeftWidth))]
    [NotifyPropertyChangedFor(nameof(PartsPanelSizable))]
    [NotifyPropertyChangedFor(nameof(LeftColumnSizable))]
    private bool panelOpen = true;

    /// <summary>Whether the parameters under the panel are unfolded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParametersHeight))]
    [NotifyPropertyChangedFor(nameof(PanelParametersSizable))]
    private bool parametersOpen = true;

    /// <summary>The version as it was last written, so a hand-typed one is left alone.</summary>
    private string _versionWritten = "";

    /// <summary>
    /// The next version up: one hundredth, and the hundredth one is the next whole number.
    /// </summary>
    /// <remarks>
    /// Two numbers and nothing else. A version anybody has written something else in, a date,
    /// a word, a third number, is handed back untouched: it says something this cannot work
    /// out, and quietly turning it into 1.01 would throw that away.
    /// </remarks>
    private static string Bumped(string version)
    {
        var parts = version.Split('.');

        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor))
            return version;

        minor++;

        if (minor > 99)
        {
            major++;
            minor = 0;
        }

        return major.ToString(CultureInfo.InvariantCulture) + "."
             + minor.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Whether the machine's own details are unfolded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RightWidth))]
    [NotifyPropertyChangedFor(nameof(MachinePickedSizable))]
    [NotifyPropertyChangedFor(nameof(RightColumnSizable))]
    private bool machineOpen = true;

    /// <summary>Whether what is picked is unfolded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PickedHeight))]
    [NotifyPropertyChangedFor(nameof(RightWidth))]
    [NotifyPropertyChangedFor(nameof(MachinePickedSizable))]
    [NotifyPropertyChangedFor(nameof(RightColumnSizable))]
    private bool pickedOpen = true;

    /// <summary>
    /// The room the parts library asks for.
    /// </summary>
    /// <remarks>
    /// A row measured in stars keeps its share of the height whatever is in it, so a card folded
    /// inside one would be a title with an empty card hanging under it. Auto is what makes a
    /// fold look like a fold: the row shrinks to the title and whatever is beside it takes the
    /// rest. The sizes these start at are the ones the editor was laid out with in the first
    /// place.
    ///
    /// Written to as well as read, because a handle dragged between two cards sets the size on
    /// the row itself. Taking it back here is what lets a card fold and open again at the size
    /// it was left at rather than at the size it was born with. The Auto that folds it is not
    /// kept, or opening it again would open it onto nothing.
    /// </remarks>
    public GridLength PartsHeight
    {
        get => PartsOpen ? _partsHeight : GridLength.Auto;
        set { if (!value.IsAuto) _partsHeight = value; }
    }

    /// <inheritdoc cref="PartsHeight"/>
    private GridLength _partsHeight = new(2, GridUnitType.Star);

    /// <summary>The room the panel list asks for.</summary>
    public GridLength PanelHeight
    {
        get => PanelOpen ? _panelHeight : GridLength.Auto;
        set { if (!value.IsAuto) _panelHeight = value; }
    }

    /// <inheritdoc cref="PanelHeight"/>
    private GridLength _panelHeight = new(1, GridUnitType.Star);

    /// <summary>The room the parameters ask for, which the panel above them takes when they fold.</summary>
    public GridLength ParametersHeight
    {
        get => ParametersOpen ? _parametersHeight : GridLength.Auto;
        set { if (!value.IsAuto) _parametersHeight = value; }
    }

    /// <inheritdoc cref="ParametersHeight"/>
    private GridLength _parametersHeight = new(220);

    /// <summary>The room what is picked asks for.</summary>
    public GridLength PickedHeight
    {
        get => PickedOpen ? _pickedHeight : GridLength.Auto;
        set { if (!value.IsAuto) _pickedHeight = value; }
    }

    /// <inheritdoc cref="PickedHeight"/>
    private GridLength _pickedHeight = new(1, GridUnitType.Star);

    /// <summary>The width of the parts and panel column, or nothing but its two titles.</summary>
    public GridLength LeftWidth
    {
        get => LeftColumnSizable ? _leftWidth : GridLength.Auto;
        set { if (!value.IsAuto) _leftWidth = value; }
    }

    /// <inheritdoc cref="LeftWidth"/>
    private GridLength _leftWidth = new(230);

    /// <summary>The width of the details column, or nothing but its two titles.</summary>
    public GridLength RightWidth
    {
        get => RightColumnSizable ? _rightWidth : GridLength.Auto;
        set { if (!value.IsAuto) _rightWidth = value; }
    }

    /// <inheritdoc cref="RightWidth"/>
    private GridLength _rightWidth = new(330);

    /// <summary>
    /// True while both cards either side of the parts handle are open.
    /// </summary>
    /// <remarks>
    /// A handle answers by putting a size on the two it lies between, which would undo a fold
    /// the moment it was dragged. It goes away with the card instead. There is nothing to share
    /// out when only one of the two is showing anyway.
    /// </remarks>
    public bool PartsPanelSizable => PartsOpen && PanelOpen;

    /// <summary>True while the parameters are open for the handle above them to share out.</summary>
    public bool PanelParametersSizable => ParametersOpen;

    /// <summary>True while both cards either side of the details handle are open.</summary>
    public bool MachinePickedSizable => MachineOpen && PickedOpen;

    /// <summary>True while the parts and panel column has anything open in it.</summary>
    public bool LeftColumnSizable => PartsOpen || PanelOpen;

    /// <summary>True while the details column has anything open in it.</summary>
    public bool RightColumnSizable => MachineOpen || PickedOpen;

    /// <summary>
    /// The panel, as a tree with the root at the top of it.
    /// </summary>
    /// <remarks>
    /// One item, always: a panel has exactly one root, and a tree control wants a collection to
    /// start from rather than a single item. Holding the root in a collection also means opening
    /// another project is emptying and refilling one list, so nothing bound to it goes stale.
    /// </remarks>
    public ObservableCollection<MachineElementViewModel> Elements { get; } = new();

    /// <summary>What is picked out in the tree, and what the inspector is showing.</summary>
    /// <remarks>
    /// Null is the ordinary state on opening rather than something to be avoided. Adding with
    /// nothing picked puts the element at the root, which is what the first element of a new
    /// panel wants anyway.
    /// </remarks>
    [ObservableProperty]
    private MachineElementViewModel? selectedElement;

    /// <summary>Every line of the panel list, however deep, for anything that has to touch all of them.</summary>
    public IEnumerable<MachineElementViewModel> Every()
    {
        foreach (var top in Elements)
        {
            foreach (var one in Below(top)) yield return one;
        }
    }

    /// <summary>That element and everything under it, depth first.</summary>
    private static IEnumerable<MachineElementViewModel> Below(MachineElementViewModel element)
    {
        yield return element;

        foreach (var child in element.Children)
        {
            foreach (var one in Below(child)) yield return one;
        }
    }

    /// <summary>
    /// The kinds of element that can be put on a panel here.
    /// </summary>
    /// <remarks>
    /// Written out rather than found by looking over the constants, so that the order is the one
    /// that suits somebody building a panel, containers first and controls after, and so that a
    /// constant added for a control this designer cannot yet place does not turn up in the list
    /// on its own.
    /// </remarks>
    public IReadOnlyList<string> Library { get; } = new[]
    {
        MachineElementKinds.Grid,
        MachineElementKinds.Group,
        MachineElementKinds.Row,
        MachineElementKinds.Column,
        MachineElementKinds.Strip,
        MachineElementKinds.Knob,
        MachineElementKinds.Fader,
        MachineElementKinds.Switch,
        MachineElementKinds.Number,
        MachineElementKinds.Button,
        MachineElementKinds.Choice,
        MachineElementKinds.Led,
        MachineElementKinds.Meter,
        MachineElementKinds.Keys,
        MachineElementKinds.Location,
        MachineElementKinds.Wave,
        MachineElementKinds.Envelope,
        MachineElementKinds.Scope,
        MachineElementKinds.Image,
        MachineElementKinds.Take,
        MachineElementKinds.Preset,
        MachineElementKinds.Pads,
        MachineElementKinds.Pad,
        MachineElementKinds.PadPicker,
        MachineElementKinds.Zones,
        MachineElementKinds.ZonePicker,
        MachineElementKinds.Slices,
        MachineElementKinds.Menu,
        MachineElementKinds.Label,
        MachineElementKinds.Text,
        MachineElementKinds.Spacer
    };

    /// <summary>
    /// Puts an element of that kind inside whatever is picked out.
    /// </summary>
    /// <remarks>
    /// Into the root when nothing is picked, because refusing would only mean telling somebody
    /// to click the one element that is certainly there. What has just been added becomes the
    /// selection, since the next thing anybody does to a new element is set it up.
    ///
    /// The panel goes back to being laid out, because adding a part is laying the panel out.
    /// Otherwise what has just been added cannot be picked, and the outline that says which one
    /// it is never appears.
    ///
    /// Always enabled; with nothing open it does nothing.
    /// </remarks>
    public IRelayCommand<string> AddElementCommand => new RelayCommand<string>(kind =>
    {
        if (Project == null || string.IsNullOrWhiteSpace(kind)) return;

        var into = SelectedElement ?? Elements.FirstOrDefault();

        if (into == null) return;

        if (Spare(kind!) is { } refused)
        {
            Status = refused;

            return;
        }

        SelectedElement = into.Add(Celled(into, Part(kind!)));

        Designing = true;

        Status = "Added " + kind + ".";
    });

    /// <summary>
    /// Moves something already on the machine inside something else on it.
    /// </summary>
    /// <remarks>
    /// The other half of dropping a part: a panel is built by putting things where they go, and
    /// getting it right first time is not how anybody lays one out. What moves is the element
    /// itself, with its settings and everything inside it, so a group full of knobs travels as
    /// a group full of knobs.
    ///
    /// Three things are refused, all for the same reason: they would leave a tree that is not a
    /// tree. The outermost element cannot be moved, because it is what everything else is inside.
    /// Nothing can be put inside itself. And nothing can be put inside something it already
    /// contains, which is the one that is not obvious: a row dropped on a knob it holds would
    /// take the knob with it and leave both pointing at each other.
    ///
    /// Landing on something that holds nothing, a knob or a picture, means the thing that holds
    /// it, which is the same rule a part off the library follows and the one that makes dropping
    /// on a crowded panel work at all.
    ///
    /// Moving something inside what it is already in is reordering it, which is the ordinary way
    /// of saying "after that one". It is taken out first so that the place it is going to is
    /// counted without it, which is what somebody dragging it there is looking at.
    /// </remarks>
    public void MoveInto(MachineElement moved, MachineElement? onto, int at = -1)
    {
        if (Project == null) return;

        if (Find(Elements, moved) is not { Parent: { } from } wrapped) return;

        var target = onto == null ? Elements.FirstOrDefault() : Find(Elements, onto);

        if (target == null) return;

        if (!Holds(target.Kind)) target = target.Parent;

        if (target == null) return;

        if (ReferenceEquals(target, wrapped) || Inside(wrapped, target))
        {
            Status = "A part cannot go inside itself.";
            return;
        }

        bool same = ReferenceEquals(target, from);

        int was = from.Children.IndexOf(wrapped);

        if (!from.Remove(wrapped)) return;

        int place = same && at > was ? at - 1 : at;

        Celled(target, moved);

        target.Put(wrapped, place);

        SelectedElement = wrapped;

        Redraw();

        Status = same
            ? "Moved the " + moved.Element + "."
            : "Moved the " + moved.Element + " into the " + target.Kind + ".";
    }

    /// <summary>Every picture an element and everything inside it names.</summary>
    private static List<string> Pictures(MachineElement element)
    {
        var found = new List<string>();

        Gather(element, found);

        return found;
    }

    /// <summary>Adds every picture that element names to the list, and then its children's.</summary>
    private static void Gather(MachineElement element, List<string> into)
    {
        if (element.Element == MachineElementKinds.Image &&
            element.Properties.TryGetValue("file", out var named) &&
            named.Length > 0)
        {
            into.Add(named);
        }

        foreach (var child in element.Children) Gather(child, into);
    }

    /// <summary>
    /// Deletes the pictures nothing on the panel shows any more, and says how many went.
    /// </summary>
    /// <remarks>
    /// Asked of what is left rather than of what was removed, because the same picture can be on
    /// a panel twice and removing one of the two is not removing the picture. The file goes only
    /// when the last element naming it has gone.
    /// </remarks>
    private int Forget(IEnumerable<string> pictures)
    {
        if (Project is not { IsSaved: true } project) return 0;

        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (project.Panel.Root is { } root) foreach (var one in Pictures(root)) kept.Add(one);

        int gone = 0;

        foreach (var picture in pictures.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (kept.Contains(picture)) continue;

            if (project.RemoveImage(picture)) gone++;
        }

        return gone;
    }

    /// <summary>
    /// Makes the pictures on disc and the pictures the panel names one and the same.
    /// </summary>
    /// <remarks>
    /// Two rules, and they are the same rule said twice: nothing in the folder that the machine
    /// does not show, and no gaps in the numbering. A machine is handed over as a folder, and
    /// image1 with image3 beside it is a machine that has plainly lost something even when it
    /// has not.
    /// </remarks>
    private void Tidy()
    {
        if (Project is not { IsSaved: true } project) return;

        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (project.Panel.Root is { } root) foreach (var one in Pictures(root)) kept.Add(one);

        project.SweepImages(kept);

        Renumbered();
    }

    /// <summary>
    /// Renumbers what is left and points every element at its picture's new name.
    /// </summary>
    /// <remarks>
    /// The files move first and the panel follows, because the folder is the thing that has to
    /// be right: a machine is handed over as a folder, and an element pointing at a name nothing
    /// on disc has is a picture that does not draw on somebody else's rack.
    ///
    /// Only what actually moved is rewritten. A machine whose pictures were already in order
    /// gets nothing done to it at all, which is the ordinary case.
    /// </remarks>
    private void Renumbered()
    {
        if (Project is not { IsSaved: true } project) return;

        var moved = project.RenumberImages();

        if (moved.Count == 0) return;

        if (project.Panel.Root is { } root) Rename(root, moved);

        foreach (var row in Every()) row.Reread();

        Redraw();
    }

    /// <summary>Points every picture element at its file's new name, however deep it sits.</summary>
    private static void Rename(MachineElement element, IReadOnlyDictionary<string, string> moved)
    {
        if (element.Element == MachineElementKinds.Image &&
            element.Properties.TryGetValue("file", out var named) &&
            moved.TryGetValue(named, out var now))
        {
            element.Properties["file"] = now;
        }

        foreach (var child in element.Children) Rename(child, moved);
    }

    /// <summary>"the picture" or "3 pictures", for a line somebody reads once.</summary>
    private static string Counted(int pictures) =>
        pictures == 1 ? "its picture" : pictures + " pictures";

    /// <summary>Whether that element is somewhere inside this one, however deep.</summary>
    private static bool Inside(MachineElementViewModel holder, MachineElementViewModel wanted)
    {
        for (var at = wanted.Parent; at != null; at = at.Parent)
        {
            if (ReferenceEquals(at, holder)) return true;
        }

        return false;
    }

    /// <summary>
    /// Takes the size somebody dragged an element out to.
    /// </summary>
    /// <remarks>
    /// The panel has already written the width and the height onto the element and drawn it, so
    /// there is nothing here to apply. What is left is everything else that was showing the old
    /// size: the row in the inspector, and the fact that the project now has something to save.
    /// </remarks>
    public void Resized(MachineElement element)
    {
        if (Find(Elements, element) is { } wrapped) wrapped.Reread();

        Status = "Sized the " + element.Element + ".";
    }

    /// <summary>
    /// Puts something arriving in a grid in a cell of its own.
    /// </summary>
    /// <remarks>
    /// A grid puts what it holds where each thing says, and something that says nothing goes in
    /// the first cell. Two of those are drawn on top of each other, which looks like the panel
    /// is broken rather than like a question nobody has answered yet. So a part landing in a
    /// grid is given the next cell nothing is in, reading across the row and then down, which
    /// is the order somebody filling a grid is already thinking in.
    ///
    /// Only where it lands in a grid, and only when it has not been placed already: a part moved
    /// out of one grid and into another keeps nothing, but one being moved about inside the same
    /// grid is being put somewhere on purpose.
    ///
    /// The number of columns comes off the grid itself. A grid that has not said how wide it is
    /// gets one column, so parts stack downwards, which is at least a shape you can see.
    /// </remarks>
    private static MachineElement Celled(MachineElementViewModel into, MachineElement arriving)
    {
        if (into.Kind != MachineElementKinds.Grid) return arriving;

        int columns = Math.Max(1, Split(into.Element, "columns"));

        var taken = new HashSet<(int, int)>();

        foreach (var child in into.Element.Children)
        {
            if (ReferenceEquals(child, arriving)) continue;

            taken.Add((Whole(child, "row"), Whole(child, "column")));
        }

        for (int at = 0; at < MostCells; at++)
        {
            var cell = (at / columns, at % columns);

            if (taken.Contains(cell)) continue;

            arriving.Properties["row"] = cell.Item1.ToString(CultureInfo.InvariantCulture);
            arriving.Properties["column"] = cell.Item2.ToString(CultureInfo.InvariantCulture);

            break;
        }

        return arriving;
    }

    /// <summary>
    /// How far the search for a free cell goes before giving up.
    /// </summary>
    /// <remarks>
    /// Far past any grid anybody would draw. It is here so that a grid whose cells have somehow
    /// all been claimed leaves the arriving part where it is rather than looping for ever.
    /// </remarks>
    private const int MostCells = 1024;

    /// <summary>How many things a comma separated property lists, or none when it says nothing.</summary>
    private static int Split(MachineElement element, string key) =>
        element.Properties.TryGetValue(key, out var said) && said.Length > 0
            ? said.Split(',').Length
            : 0;

    /// <summary>A whole number a property holds, or nothing at all when it holds something else.</summary>
    private static int Whole(MachineElement element, string key) =>
        element.Properties.TryGetValue(key, out var said) &&
        int.TryParse(said, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;

    /// <summary>
    /// A new part of that kind, with the properties it is no use without already on it.
    /// </summary>
    /// <remarks>
    /// A part arrives with an empty property list, and for most kinds that is right: a knob
    /// knows its own size and a row spaces itself. A picture does not. It has no size of its
    /// own that means anything on a panel, so the two rows somebody will certainly want are put
    /// there to be typed over rather than added by hand from a blank list, which is how you find
    /// out that the property is called "width" only by reading somebody else's machine.
    ///
    /// Values, not placeholders: a picture 120 by 60 is a picture you can see and then adjust,
    /// and an empty width is a picture the panel has to guess about.
    ///
    /// A grid is the one container you have to say the shape of, and one that arrives saying
    /// nothing looks broken: everything dropped in it lands in the same cell. Two by two to start
    /// with, which is the smallest shape that is plainly a grid.
    ///
    /// A group is a frame with a name on it, and the name is the whole point of choosing one over
    /// a plain row, so it starts with a word to type over rather than an empty frame.
    ///
    /// A grid of pads arrives four across with the cap a hand can hit, which is the shape a drum
    /// machine's pads have had since they were made of rubber; as one pad in a column it would be
    /// a grid nobody could recognise as pads. Its buttons are keyed from
    /// <see cref="FirstPadKey"/>, because a grid that arrives with no buttons is a grid nobody
    /// can see, and it is laid out again from the boxes beside the panel the moment it is meant
    /// to be a different shape.
    ///
    /// A single pad, for adding to a grid that is nearly right, arrives with a key rather than
    /// empty: its key is what a preset writes its line against.
    /// </remarks>
    private static MachineElement Part(string kind)
    {
        var made = new MachineElement { Element = kind };

        if (kind == MachineElementKinds.Image)
        {
            made.Properties["width"] = "120";
            made.Properties["height"] = "60";
        }

        if (kind == MachineElementKinds.Grid)
        {
            made.Properties["rows"] = "Auto,Auto";
            made.Properties["columns"] = "Auto,Auto";
        }

        if (kind == MachineElementKinds.Group) made.Properties["caption"] = "Group";

        if (kind == MachineElementKinds.Pads)
        {
            made.Properties["rows"] = "4";
            made.Properties["columns"] = "4";
            made.Properties["cap"] = "86";
            made.Properties["capHeight"] = "42";

            for (int at = 0; at < FreshPads; at++)
            {
                made.Children.Add(new MachineElement
                {
                    Element = MachineElementKinds.Pad,
                    Parameter = "pad" + (at + 1),
                    Properties = { ["key"] = (FirstPadKey + at).ToString(CultureInfo.InvariantCulture) },
                });
            }
        }

        if (kind == MachineElementKinds.Pad)
            made.Properties["key"] = FirstPadKey.ToString(CultureInfo.InvariantCulture);

        return made;
    }

    /// <summary>The key a fresh grid's first button answers to, which is C-4.</summary>
    /// <remarks>
    /// The middle of the keyboard, so a kit laid out here is playable from the octave a keyboard
    /// opens on rather than from an end of it.
    /// </remarks>
    private const int FirstPadKey = 48;

    /// <summary>How many buttons a fresh grid of pads arrives with.</summary>
    /// <remarks>
    /// Four by four, the shape a drum machine's pads have had since they were made of rubber, and
    /// laid out again from the boxes beside the panel the moment it is meant to be another shape.
    /// </remarks>
    private const int FreshPads = 16;

    /// <summary>
    /// Takes the picked element out, and everything inside it with it.
    /// </summary>
    /// <remarks>
    /// The root stays, because a panel with no root is not a panel and there would be nothing
    /// left to add to. The selection moves to whatever held the element, so a run of removals
    /// works upwards instead of stopping dead.
    ///
    /// What the element and everything inside it was showing is read before it goes, since
    /// afterwards there is nothing left to ask. The numbers then close up behind what went, so the
    /// folder reads image1, image2 with nothing missing, and the panel is told what its pictures
    /// are called now.
    ///
    /// Always enabled; the root refuses with a reason rather than being greyed.
    /// </remarks>
    public IRelayCommand RemoveElementCommand => new RelayCommand(() =>
    {
        var element = SelectedElement;

        if (element?.Parent is not { } parent)
        {
            if (element != null) Status = "The panel's outermost element cannot be removed.";
            return;
        }

        var pictures = Pictures(element.Element);

        if (!parent.Remove(element)) return;

        SelectedElement = parent;

        int dropped = Forget(pictures);

        if (dropped > 0) Renumbered();

        Status = dropped == 0
            ? "Removed the " + element.Kind + "."
            : "Removed the " + element.Kind + ", and " + Counted(dropped) + " with it.";
    });

    /// <summary>Moves the picked element one place earlier among the things beside it.</summary>
    /// <remarks>
    /// Always enabled. A step that would run off the end does nothing rather than wrapping, which
    /// is what a button held down expects.
    /// </remarks>
    public IRelayCommand MoveUpCommand => new RelayCommand(() => Shift(-1));

    /// <summary>Moves the picked element one place later among the things beside it.</summary>
    /// <inheritdoc cref="MoveUpCommand" path="/remarks"/>
    public IRelayCommand MoveDownCommand => new RelayCommand(() => Shift(1));

    /// <summary>
    /// Both reordering commands, which differ only in which way they step.
    /// </summary>
    /// <remarks>
    /// The order of the children is the order they are drawn in, so this is the only positioning
    /// a row or a column has. Moving out of one container into another is a different act and is
    /// not this.
    /// </remarks>
    private void Shift(int by)
    {
        if (SelectedElement?.Parent is not { } parent) return;

        parent.Move(SelectedElement, by);
    }

    /// <summary>
    /// The machine as the panel wants it: what it looks like, what it can be set to, and where
    /// it is kept, made fresh so that a change to any of the three is drawn.
    /// </summary>
    /// <remarks>
    /// A new object every time on purpose, wrapping the same root. The panel draws what it is
    /// given and draws again when it is given something else, and an element added to a tree it is
    /// already holding is not something else: without this, moving a knob in the designer would
    /// change everything except the picture of it. What gets saved is the project's own panel,
    /// which this only points at.
    /// </remarks>
    public MachineFace? Shown =>
        Project is { } project
            ? new MachineFace(new MachinePanel { Root = project.Panel.Root }, project.Parameters, project.Folder)
            : null;

    /// <summary>Just the keys, for choosing which parameter a control turns.</summary>
    public IReadOnlyList<string> ParameterKeys =>
        Project?.Parameters.Select(p => p.Key).Where(k => k.Length > 0).ToArray() ?? Array.Empty<string>();

    /// <summary>The parameters as the canvas reads them: a copy, so a change is a different list.</summary>
    public IReadOnlyList<MachineParameter> Shape =>
        Project?.Parameters.ToArray() ?? Array.Empty<MachineParameter>();

    /// <summary>
    /// What is picked, named the way the canvas names it.
    /// </summary>
    /// <remarks>
    /// The canvas knows elements and the editor knows the wrappers around them, and this is the
    /// one place the two are matched up, so a click on the panel and a click in the tree end on
    /// the same thing without either side knowing the other exists.
    /// </remarks>
    public MachineElement? SelectedShape
    {
        get => SelectedElement?.Element;
        set => SelectedElement = value == null ? null : Find(Elements, value);
    }

    /// <summary>True when there is something to remove, move or set up.</summary>
    public bool HasSelection => SelectedElement != null;

    /// <summary>The wrapper standing for that element, wherever it is in the tree.</summary>
    private static MachineElementViewModel? Find(
        IEnumerable<MachineElementViewModel> among, MachineElement wanted)
    {
        foreach (var one in among)
        {
            if (ReferenceEquals(one.Element, wanted)) return one;

            if (Find(one.Children, wanted) is { } found) return found;
        }

        return null;
    }

    /// <summary>
    /// Puts an element of that kind into whatever it was dropped on.
    /// </summary>
    /// <remarks>
    /// Dropped on something that holds nothing, a knob say, it goes beside it rather than
    /// inside it, since a knob has no inside. That is what somebody dropping onto a full row
    /// means, and refusing would leave the only place to drop a control the gaps between the
    /// ones already there.
    /// </remarks>
    public void Drop(string kind, MachineElement? onto, int at = -1)
    {
        if (Project == null || string.IsNullOrWhiteSpace(kind)) return;

        var target = onto == null ? Elements.FirstOrDefault() : Find(Elements, onto);

        if (target == null) return;

        if (!Holds(target.Kind)) target = target.Parent ?? Elements.FirstOrDefault();

        if (target == null) return;

        if (Spare(kind) is { } refused)
        {
            Status = refused;

            return;
        }

        SelectedElement = target.Put(Celled(target, Part(kind)), at);

        Designing = true;

        Status = "Added " + kind + ".";
    }

    /// <summary>
    /// Why that part cannot be added, or nothing when it can.
    /// </summary>
    /// <remarks>
    /// One menu to a machine, and it is the only part with a limit. A menu is drawn over the
    /// panel in the corner it names rather than laid out with the controls, so a second one is
    /// either in the same corner drawing over the first or in another corner offering the same
    /// lines twice. Neither is a thing anybody meant, and both are the kind of mistake you only
    /// notice with the designer shut.
    ///
    /// Said rather than silently dropped, and said in terms of what to do instead: the one that
    /// is there is the one to move, and moving it is choosing a corner.
    /// </remarks>
    /// <param name="kind">The part being added.</param>
    private string? Spare(string kind) =>
        kind == MachineElementKinds.Menu && Every().Any(one => one.Kind == MachineElementKinds.Menu)
            ? "This machine already has a menu. There is one to a machine: pick the one it has and choose a corner for it."
            : null;

    /// <summary>Whether a kind of element has an inside to put things in.</summary>
    private static bool Holds(string kind) =>
        kind is MachineElementKinds.Grid
             or MachineElementKinds.Group
             or MachineElementKinds.Row
             or MachineElementKinds.Column
             or MachineElementKinds.Strip;

    /// <summary>
    /// Tells whatever is drawing the panel that the description under it has moved.
    /// </summary>
    /// <remarks>
    /// The elements are plain data and say nothing when they are edited, which is the price of
    /// a machine describing itself without a view model toolkit. So the editor watches its own
    /// wrappers instead and says it here, once, however the edit was made.
    ///
    /// Every edit ends here, so this is also where the history hears about it. Told more often
    /// than there are edits, which is safe: a redraw where nothing about the machine moved reads
    /// the same as before and leaves no step.
    ///
    /// What is picked can turn into a picture without the selection moving, by being changed from
    /// one kind of element into another, and the way of choosing a file has to come and go with it.
    /// </remarks>
    public void Redraw()
    {
        History.Did(Project);

        OnPropertyChanged(nameof(Shown));
        OnPropertyChanged(nameof(Shape));

        OnPropertyChanged(nameof(PicturePicked));
        OnPropertyChanged(nameof(PadsPicked));
    }

    /// <summary>
    /// The same, for a change that also alters which parameters there are to choose from.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Redraw"/> deliberately. A list of keys that is made afresh is
    /// a different list to anything showing it, and a picker rebuilt in the middle of somebody
    /// picking from it answers by picking again: putting a control on a parameter announced the
    /// keys, which rebuilt the picker, which put the control on a parameter, until the stack ran
    /// out. Only a parameter added, renamed or taken out changes the keys, so only those say so.
    /// </remarks>
    private void ParametersChanged()
    {
        Redraw();

        OnPropertyChanged(nameof(ParameterKeys));
    }

    /// <summary>The element the settings pane is showing, watched while it is showing.</summary>
    private MachineElementViewModel? _picked;

    /// <summary>
    /// The picked element said something about itself that changes the panel rather than the
    /// element.
    /// </summary>
    /// <remarks>
    /// Which of the two browsers a picker is, is the one: flip it and the control beside it is a
    /// category list that was not there a moment ago. The list behind the picker is then a
    /// different list, so the panel is drawn again against it.
    /// </remarks>
    private void Picked(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MachineElementViewModel.Source)) return;

        OnPropertyChanged(nameof(Presets));

        Redraw();

        Status = "The picker now browses " + (_picked?.Source ?? "") + ". Save when you are happy with it.";
    }

    /// <summary>
    /// Follows the picked element, opens the way down to it, and fills the grid boxes from it.
    /// </summary>
    /// <remarks>
    /// Picked on the panel, an element may be buried three branches deep in the list. Opening the
    /// way down to it is what makes the two halves the same selection rather than two.
    ///
    /// The boxes for laying a grid out start at what the picked grid already is, so pressing the
    /// button without changing anything lays out what is already there. How many rows is said by
    /// the grid, or worked out from the buttons for one written before it said so.
    /// </remarks>
    partial void OnSelectedElementChanged(MachineElementViewModel? value)
    {
        if (_picked != null) _picked.PropertyChanged -= Picked;

        _picked = value;

        if (_picked != null) _picked.PropertyChanged += Picked;

        value?.Reveal();

        OnPropertyChanged(nameof(SelectedShape));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(PicturePicked));
        OnPropertyChanged(nameof(PadsPicked));

        if (value?.Element is { } picked && picked.Element == MachineElementKinds.Pads)
        {
            int held = picked.Children.Count(child => child.Element == MachineElementKinds.Pad);

            if (int.TryParse(picked.Properties.GetValueOrDefault("columns"), out int across) && across > 0)
                PadColumns = across;

            PadRows = int.TryParse(picked.Properties.GetValueOrDefault("rows"), out int down) && down > 0
                ? down
                : Math.Max(1, (held + Math.Max(1, PadColumns) - 1) / Math.Max(1, PadColumns));

            var lowest = picked.Children.FirstOrDefault(child => child.Element == MachineElementKinds.Pad);

            if (lowest != null && lowest.Properties.GetValueOrDefault("key") is { Length: > 0 } key)
                PadFirstKey = key;
        }
    }

    /// <summary>What a picture element keeps the name of its file under.</summary>
    /// <remarks>
    /// Written out rather than built, so the one key the designer sets by itself can be found by
    /// anybody grepping for it. Every other property on every other element is typed by hand.
    /// </remarks>
    private const string FileKey = "file";

    /// <summary>
    /// True while what is picked is a picture, which is when there is a file to go and get.
    /// </summary>
    /// <remarks>
    /// A picture is an element like any other and is worked on like any other, in the list of
    /// properties beside the panel. The one thing that list cannot do is copy a file into the
    /// machine, so that, and only that, is offered separately, and only while the thing it would
    /// act on is what is picked.
    /// </remarks>
    public bool PicturePicked => SelectedElement?.Kind == MachineElementKinds.Image;

    /// <summary>True when a grid of pads is picked, which is the one shape you lay out at once.</summary>
    public bool PadsPicked => SelectedElement?.Kind == MachineElementKinds.Pads;

    /// <summary>
    /// How many rows of buttons the grid has, and how many stand side by side.
    /// </summary>
    /// <remarks>
    /// A grid is said the way a grid is said: four by four, or six by sixteen. How many buttons
    /// there are in total is those two multiplied, which is not a thing anybody should have to
    /// work out in their head before typing it.
    /// </remarks>
    [ObservableProperty] private int padRows = 4;

    /// <inheritdoc cref="PadRows"/>
    [ObservableProperty] private int padColumns = 4;

    /// <summary>
    /// The key the first one answers to, the rest running up from it.
    /// </summary>
    /// <remarks>
    /// A note, because that is what a machine writes on its buttons and what a preset is keyed
    /// by. A plain number is taken as well, since somebody may reasonably type 48.
    /// </remarks>
    [ObservableProperty] private string padFirstKey = "C-4";

    /// <summary>The most buttons a grid may have on a side.</summary>
    /// <remarks>
    /// Not a shape anybody would draw, and that is the point: it is here so a number typed with an
    /// extra digit in it cannot ask for a million buttons and take the window with it.
    /// </remarks>
    private const int MostPads = 64;

    /// <summary>
    /// Lays the picked grid out: that many buttons, that many across, keyed from there.
    /// </summary>
    /// <remarks>
    /// The buttons are the truth and this is a tool that writes them, which is why it is here
    /// and not a property the panel reads. Ninety six buttons dropped one at a time is not a
    /// designer, and a count the drawing obeyed instead of the buttons would be a second place
    /// the number lived.
    ///
    /// Names already in the grid are kept, in order, because renaming a pad is work somebody
    /// did: laying the grid out again to add a row must not throw away the first sixteen names.
    ///
    /// The keys are written as notes, which is what the machine's buttons hold and what its
    /// presets are keyed by. A grid keyed in numbers would leave every preset naming a button that
    /// is not there.
    ///
    /// The grid is told both its rows and its columns, because neither can be worked out from the
    /// buttons: sixteen of them is four by four, two by eight or sixteen by one.
    ///
    /// Always enabled; anything but a grid of pads is refused, and <see cref="PadsPicked"/> is
    /// what the button's own look is bound to.
    /// </remarks>
    public IRelayCommand LayPadsCommand => new RelayCommand(() =>
    {
        if (SelectedElement?.Element is not { } pads) return;

        if (pads.Element != MachineElementKinds.Pads) return;

        int across = Math.Clamp(PadColumns, 1, MostPads);
        int down = Math.Clamp(PadRows, 1, MostPads);
        int wanted = across * down;

        int first = _notes.Semitone(PadFirstKey);

        if (first < 0) first = FirstPadKey;

        var kept = pads.Children
            .Where(child => child.Element == MachineElementKinds.Pad)
            .Select(child => child.Parameter)
            .ToList();

        pads.Children.Clear();

        for (int at = 0; at < wanted; at++)
        {
            pads.Children.Add(new MachineElement
            {
                Element = MachineElementKinds.Pad,
                Parameter = at < kept.Count && kept[at].Length > 0 ? kept[at] : "pad" + (at + 1),
                Properties = { ["key"] = _notes.Name(first + at) },
            });
        }

        pads.Properties["rows"] = down.ToString(CultureInfo.InvariantCulture);
        pads.Properties["columns"] = across.ToString(CultureInfo.InvariantCulture);

        Redraw();

        Status = down + " by " + across + ", " + wanted + " buttons";
    });

    /// <summary>
    /// Brings a picture into the machine and puts its name on the element that is picked.
    /// </summary>
    /// <remarks>
    /// Two things at once on purpose, because they are one act: a picture that had been copied
    /// in but not named would show nowhere, and a name typed for a file that is not in the
    /// folder is the broken picture this is here to avoid.
    ///
    /// A machine that has never been saved has no folder to keep anything in, and inventing one
    /// would be choosing where somebody's work lives on their behalf. So it says so instead.
    /// </remarks>
    public void PutPicture(string path)
    {
        if (SelectedElement is not { Kind: MachineElementKinds.Image } element) return;

        if (Project is not { IsSaved: true } project)
        {
            Status = "Save the machine somewhere first. Its pictures live in its own folder.";
            return;
        }

        try
        {
            if (project.AddImage(path) is not { Length: > 0 } named)
            {
                Status = "That picture could not be brought in.";
                return;
            }

            Put(element, FileKey, named);

            Status = "The picture is in the machine, as " + named + ".";
        }
        catch (Exception ex)
        {
            Status = "Could not add the picture: " + ex.Message;
        }
    }

    /// <summary>
    /// Writes a property on an element, adding the row for it when there is not one already.
    /// </summary>
    /// <remarks>
    /// Both halves, because the inspector shows the rows and the element holds the dictionary,
    /// and a value written into the dictionary alone is a value nobody can see or correct. The
    /// row that already exists is written through rather than replaced, so somebody who had put
    /// the key there themselves keeps the row they were typing in.
    /// </remarks>
    private static void Put(MachineElementViewModel element, string key, string value)
    {
        if (element.Properties.FirstOrDefault(row => row.Key == key) is { } already)
        {
            already.Value = value;
            return;
        }

        element.Element.Properties[key] = value;

        element.Properties.Add(new MachineElementPropertyViewModel(element.Element, key));
    }

    /// <summary>
    /// Listens to an element and everything under it, so any edit reaches the canvas.
    /// </summary>
    /// <remarks>
    /// The whole subtree at once, and anything added later as it arrives. Nothing is
    /// unsubscribed when a project is closed: the wrappers are thrown away whole, and an event
    /// holds the listener rather than the other way about, so the old tree keeps nothing alive.
    /// </remarks>
    private void Watch(MachineElementViewModel element)
    {
        element.PropertyChanged += Edited;
        element.Children.CollectionChanged += Grew;
        element.Properties.CollectionChanged += Grew;

        foreach (var property in element.Properties) property.PropertyChanged += Edited;

        foreach (var child in element.Children) Watch(child);
    }

    /// <summary>Something was added to a watched list, so it is watched too and the panel redrawn.</summary>
    private void Grew(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var added in e.NewItems ?? (System.Collections.IList)Array.Empty<object>())
        {
            if (added is MachineElementViewModel element) Watch(element);
            else if (added is MachineElementPropertyViewModel property) property.PropertyChanged += Edited;
        }

        Redraw();
    }

    /// <summary>
    /// One field of an element or a parameter, changed.
    /// </summary>
    /// <remarks>
    /// A knob being turned in the preview is left out. It reaches here as a parameter's Value,
    /// and drawing the panel again in the middle of a drag would take the knob out from under
    /// the pointer.
    ///
    /// Which is a thing about parameters and not about the word Value. A property on an element
    /// is a key and a value like any other pair, and its value is what the panel is drawn from:
    /// left out by its name alone, a picture given a file, a group given a caption and a grid
    /// given its columns would all sit there unchanged until something else happened to redraw
    /// the panel.
    /// </remarks>
    private void Edited(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is MachineParameterViewModel)
        {
            if (e.PropertyName is nameof(MachineParameterViewModel.Value) or nameof(MachineParameterViewModel.On)) return;

            ParametersChanged();
            return;
        }

        Redraw();
    }

    /// <summary>
    /// Hangs the editor's wrappers off a project's tree again.
    /// </summary>
    /// <remarks>
    /// Called when a machine is opened, and again when a step is put back: an undo replaces the
    /// panel and the parameters inside the project that is already open, and every wrapper on
    /// screen is still pointed at the elements that were there before.
    /// </remarks>
    public void Rewrap() => Wrap(Project);

    /// <summary>Told by the history, so the buttons that depend on it are redrawn.</summary>
    private void HistoryMoved()
    {
        OnPropertyChanged(nameof(NeedsSaving));
        OnPropertyChanged(nameof(CanCancelChanges));
    }

    /// <summary>
    /// A different machine was opened, so the history starts again and the wrappers are rebuilt.
    /// </summary>
    /// <remarks>
    /// Only when a different machine is opened, and not when a step is put back into the one that
    /// already is: putting a step back rebuilds the same wrappers over the same project through
    /// <see cref="Rewrap"/>, and a history that emptied itself on the way would undo once and then
    /// refuse to undo again.
    /// </remarks>
    partial void OnProjectChanged(MachineProject? value)
    {
        History.Opened(value);

        Wrap(value);
    }

    /// <summary>
    /// Hangs a fresh set of wrappers off a project's parameters and its panel.
    /// </summary>
    /// <remarks>
    /// The tools work on whichever machine is open, so opening one is what they hear. Asked
    /// gently, because this can run before the field has been given anything: a machine opened
    /// during construction would otherwise reach a tool set that does not exist yet.
    ///
    /// The version as the file says it is kept, so the first save of a machine that was only
    /// looked at still counts as a change, and one whose version was typed over does not get
    /// bumped as well.
    /// </remarks>
    private void Wrap(MachineProject? value)
    {
        Utilities?.Reread();

        Parameters.Clear();
        Elements.Clear();
        SelectedElement = null;

        _versionWritten = value?.Version ?? "";

        if (value == null) return;

        foreach (var parameter in value.Parameters)
        {
            var wrapped = new MachineParameterViewModel(parameter);

            wrapped.PropertyChanged += Edited;

            Parameters.Add(wrapped);
        }

        var root = new MachineElementViewModel(Root(value), edited: Redraw);

        Watch(root);

        Elements.Add(root);

        ParametersChanged();
    }

    /// <summary>
    /// The project's root element, made if the file did not have one.
    /// </summary>
    /// <remarks>
    /// A machine saved by something else, or by hand, can arrive with no panel at all or with a
    /// root that says nothing. There has to be something to hang the first element off, so a
    /// grid is put in and written back into the project, which means saving keeps it.
    /// </remarks>
    private static MachineElement Root(MachineProject project)
    {
        project.Panel ??= new MachinePanel();
        project.Panel.Root ??= new MachineElement { Element = MachineElementKinds.Grid };

        if (project.Panel.Root.Element.Length == 0) project.Panel.Root.Element = MachineElementKinds.Grid;

        return project.Panel.Root;
    }
}
