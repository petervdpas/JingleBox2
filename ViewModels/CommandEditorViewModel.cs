using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker.Commands;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels.Enums;
using JingleBox2.ViewModels.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// The command popup: a pattern command chosen by name and set by a drawn ruler, a chord or a
/// slider, rather than typed as a letter and two hex digits.
/// </summary>
/// <remarks>
/// A second way in to the same cell and nothing more: what it hands back is the
/// <see cref="TrackerCommand"/> typing would have written, so the two cannot disagree. It opens on
/// whatever the cell already holds, read back into the controls, so opening it on a command and
/// pressing OK changes nothing.
///
/// The ruler is the line's twelve ticks. For a delay or a cut, clicking a tick is the amount; for a
/// retrigger and an arpeggio it shows what the line will do, worked out by the commands module
/// itself so the picture is what is played.
/// </remarks>
public sealed partial class CommandEditorViewModel : ObservableObject
{
    /// <summary>The commands offered, in the order their buttons stand.</summary>
    public IReadOnlyList<CommandChoice> Choices { get; } = new[]
    {
        new CommandChoice(CommandKind.Delay, "Delay", TrackerCommand.Delay, "Starts the note later in its line."),
        new CommandChoice(CommandKind.Cut, "Cut", TrackerCommand.Cut, "Silences the note partway through its line, with no release."),
        new CommandChoice(CommandKind.Retrigger, "Retrigger", TrackerCommand.Retrigger, "Plays the note again within its line, for rolls and stutters."),
        new CommandChoice(CommandKind.Arpeggio, "Arpeggio", TrackerCommand.Arpeggio, "Steps the note through a chord, one step a tick."),
        new CommandChoice(CommandKind.Glide, "Glide", TrackerCommand.Glide, "Slides from the note already sounding to this one, without starting it again."),
        new CommandChoice(CommandKind.Offset, "Offset", TrackerCommand.Offset, "Starts a recording partway in, so one break can be played as many hits."),
        new CommandChoice(CommandKind.Volume, "Volume", TrackerCommand.SetVolume, "Sets this note's own volume."),
        new CommandChoice(CommandKind.Pan, "Pan", TrackerCommand.SetPan, "Places this note left or right."),
        new CommandChoice(CommandKind.Tempo, "Tempo", TrackerCommand.Tempo, "Changes how fast the song plays from this line, as a step in the song's tempo lane.")
    };

    /// <summary>The chords an arpeggio offers by name.</summary>
    public IReadOnlyList<ArpChord> Chords { get; } = new[]
    {
        new ArpChord("Major", 4, 7),
        new ArpChord("Minor", 3, 7),
        new ArpChord("Sus2", 2, 7),
        new ArpChord("Sus4", 5, 7),
        new ArpChord("Diminished", 3, 6),
        new ArpChord("Augmented", 4, 8),
        new ArpChord("Power", 7, 12),
        new ArpChord("Octave", 12, 0)
    };

    /// <summary>How many ticks a line has, which is what the ruler draws.</summary>
    public int TicksPerLine { get; }

    /// <summary>The highest tick, for the ruler's end.</summary>
    public int LastTick => TicksPerLine - 1;

    /// <summary>Works out what a retrigger or an arpeggio does over a line, for the ruler.</summary>
    private readonly NoteRetrigger _retrigger = new();

    /// <summary>The same for an arpeggio.</summary>
    private readonly NoteArpeggio _arpeggio = new();

    /// <summary>The command chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kind), nameof(IsTimed), nameof(IsRetrigger), nameof(IsArpeggio),
        nameof(IsGlide), nameof(IsOffset), nameof(IsVolume), nameof(IsPan), nameof(IsTempo), nameof(ShowsRuler))]
    private CommandChoice chosen;

    /// <summary>For a delay or a cut, which tick it happens on.</summary>
    [ObservableProperty] private int tick;

    /// <summary>For a retrigger, every how many ticks the note plays again.</summary>
    [ObservableProperty] private double every = 4;

    /// <summary>For a retrigger, how many sixteenths quieter each repeat is.</summary>
    [ObservableProperty] private double fall;

    /// <summary>For an arpeggio, the first interval above the note.</summary>
    [ObservableProperty] private double up = 4;

    /// <summary>For an arpeggio, the second interval above the note.</summary>
    [ObservableProperty] private double up2 = 7;

    /// <summary>For a glide, how many ticks the slide takes; nought is one line.</summary>
    [ObservableProperty] private double slide;

    /// <summary>For an offset, how far into the recording it starts, in 256ths.</summary>
    [ObservableProperty] private double skip = 0x80;

    /// <summary>For a tempo, the beats a minute, 20 to 255.</summary>
    [ObservableProperty] private double beats = TrackerTiming.DefaultBpm;

    /// <summary>For a volume, the level, 00 to 80.</summary>
    [ObservableProperty] private double level = 0x40;

    /// <summary>For a pan, the place, 00 hard left to 80 hard right.</summary>
    [ObservableProperty] private double place = 0x40;

    /// <summary>Opens on the command a cell already holds, or on a delay where it holds none or one this does not offer.</summary>
    /// <param name="current">What the cell holds.</param>
    /// <param name="ticks">How many ticks a line has.</param>
    public CommandEditorViewModel(TrackerCommand current, int ticks = LineCommands.DefaultTicks)
    {
        TicksPerLine = Math.Max(1, ticks);
        chosen = Choices[0];
        tick = TicksPerLine / 2;

        Read(current);
    }

    /// <summary>Which command is chosen.</summary>
    public CommandKind Kind => Chosen.Kind;

    /// <summary>Whether the chosen command is set by picking a tick.</summary>
    public bool IsTimed => Kind is CommandKind.Delay or CommandKind.Cut;

    /// <summary>Whether a retrigger is chosen.</summary>
    public bool IsRetrigger => Kind == CommandKind.Retrigger;

    /// <summary>Whether an arpeggio is chosen.</summary>
    public bool IsArpeggio => Kind == CommandKind.Arpeggio;

    /// <summary>Whether a glide is chosen.</summary>
    public bool IsGlide => Kind == CommandKind.Glide;

    /// <summary>Whether a sample offset is chosen.</summary>
    public bool IsOffset => Kind == CommandKind.Offset;

    /// <summary>Whether a tempo is chosen.</summary>
    public bool IsTempo => Kind == CommandKind.Tempo;

    /// <summary>Whether a volume is chosen.</summary>
    public bool IsVolume => Kind == CommandKind.Volume;

    /// <summary>Whether a pan is chosen.</summary>
    public bool IsPan => Kind == CommandKind.Pan;

    /// <summary>Whether the chosen command has anything to show on the ruler.</summary>
    public bool ShowsRuler => !IsVolume && !IsPan && !IsOffset && !IsTempo;

    /// <summary>The command as it will be written into the cell.</summary>
    public TrackerCommand Command => Kind switch
    {
        CommandKind.Delay => new TrackerCommand(TrackerCommand.Delay, Tick),
        CommandKind.Cut => new TrackerCommand(TrackerCommand.Cut, Tick),
        CommandKind.Retrigger => new TrackerCommand(TrackerCommand.Retrigger, Nibbles(Fall, Every)),
        CommandKind.Arpeggio => new TrackerCommand(TrackerCommand.Arpeggio, Nibbles(Up, Up2)),
        CommandKind.Glide => new TrackerCommand(TrackerCommand.Glide, Byte(Slide, 0xFF)),
        CommandKind.Offset => new TrackerCommand(TrackerCommand.Offset, Byte(Skip, 0xFF)),
        CommandKind.Volume => new TrackerCommand(TrackerCommand.SetVolume, Byte(Level, TrackerCell.MaxVolume)),
        CommandKind.Tempo => new TrackerCommand(TrackerCommand.Tempo, Math.Clamp((int)Math.Round(Beats), MinTempo, 0xFF)),
        _ => new TrackerCommand(TrackerCommand.SetPan, Byte(Place, TrackerCell.MaxVolume))
    };

    /// <summary>What the cell will read, such as <c>Q06</c>.</summary>
    public string Written => Command.ToString();

    /// <summary>The ticks of the line, as the ruler draws them for the chosen command.</summary>
    public IReadOnlyList<TickMark> Ruler
    {
        get
        {
            var marks = new TickMark[TicksPerLine];

            for (int at = 0; at < TicksPerLine; at++) marks[at] = new TickMark(at, false, "");

            switch (Kind)
            {
                case CommandKind.Delay:
                case CommandKind.Cut:
                    marks[Tick] = new TickMark(Tick, true, "");
                    break;

                case CommandKind.Retrigger:
                    foreach (var one in Spread(_retrigger))
                        marks[one.Tick] = new TickMark(one.Tick, true, "");
                    break;

                case CommandKind.Glide:
                    for (int at = 0; at < Math.Min(TicksPerLine, GlideTicks); at++) marks[at] = new TickMark(at, true, "");
                    break;

                case CommandKind.Arpeggio:
                    float shift = 0;
                    var steps = Spread(_arpeggio).Where(one => one.Event.Kind == Tracker.Enums.TrackerEventKind.Shift)
                        .ToDictionary(one => one.Tick, one => one.Event.Shift);

                    for (int at = 0; at < TicksPerLine; at++)
                    {
                        if (steps.TryGetValue(at, out float moved)) shift = moved;

                        marks[at] = new TickMark(at, shift != 0, shift == 0 ? "0" : "+" + shift);
                    }
                    break;
            }

            return marks;
        }
    }

    /// <summary>One sentence saying what the command will do, in words rather than digits.</summary>
    public string Explanation => Kind switch
    {
        CommandKind.Delay => Tick == 0
            ? "The note starts on its line, as if there were no delay."
            : $"The note starts {Ticks(Tick)} into its line: {Share(Tick)} late.",
        CommandKind.Cut => $"The note is cut dead {Ticks(Tick)} into its line, after {Share(Tick)} of it.",
        CommandKind.Retrigger => RetriggerWords(),
        CommandKind.Arpeggio => ArpeggioWords(),
        CommandKind.Glide => GlideWords(),
        CommandKind.Offset => $"A recording starts {Math.Round(Byte(Skip, 0xFF) / 2.56)}% of the way in. Synths and plugins are not affected.",
        CommandKind.Tempo => $"From this line the song plays at {Math.Clamp((int)Math.Round(Beats), MinTempo, 0xFF)} beats a minute. It goes into the song's tempo lane on the mixer, not into the cell, and the tempo saved with the song stays as it is.",
        CommandKind.Volume => $"This note plays at {Math.Round(Level / TrackerCell.MaxVolume * 100)}% volume, whatever the volume field says.",
        _ => PanWords()
    };

    /// <summary>The name of the chord the arpeggio's intervals make, or empty for none with a name.</summary>
    public string ChordName =>
        Chords.FirstOrDefault(chord => chord.Up == (int)Math.Round(Up) && chord.Up2 == (int)Math.Round(Up2))?.Name ?? "";

    /// <summary>Picks a tick on the ruler, which is a delay's or a cut's amount.</summary>
    [RelayCommand]
    private void PickTick(int at)
    {
        if (!IsTimed) return;

        Tick = Math.Clamp(at, 0, LastTick);
    }

    /// <summary>Sets the arpeggio's intervals to a named chord.</summary>
    [RelayCommand]
    private void PickChord(ArpChord? chord)
    {
        if (chord == null) return;

        Up = chord.Up;
        Up2 = chord.Up2;
    }

    /// <summary>Puts a command a cell already holds into the controls.</summary>
    private void Read(TrackerCommand current)
    {
        var choice = Choices.FirstOrDefault(one => one.Letter == current.Command);

        if (current.IsNone || choice == null) return;

        Chosen = choice;

        int high = (current.Parameter >> 4) & 0x0F;
        int low = current.Parameter & 0x0F;

        switch (choice.Kind)
        {
            case CommandKind.Delay:
            case CommandKind.Cut:
                Tick = Math.Clamp(current.Parameter, 0, LastTick);
                break;

            case CommandKind.Retrigger:
                Fall = high;
                Every = Math.Max(1, low);
                break;

            case CommandKind.Arpeggio:
                Up = high;
                Up2 = low;
                break;

            case CommandKind.Glide:
                Slide = Math.Clamp(current.Parameter, 0, 0xFF);
                break;

            case CommandKind.Offset:
                Skip = Math.Clamp(current.Parameter, 0, 0xFF);
                break;

            case CommandKind.Volume:
                Level = Math.Clamp(current.Parameter, 0, TrackerCell.MaxVolume);
                break;

            case CommandKind.Tempo:
                Beats = Math.Clamp(current.Parameter, MinTempo, 0xFF);
                break;

            case CommandKind.Pan:
                Place = Math.Clamp(current.Parameter, 0, TrackerCell.MaxVolume);
                break;
        }
    }

    /// <summary>Everything that follows from the command having changed.</summary>
    private void Moved()
    {
        OnPropertyChanged(nameof(Command));
        OnPropertyChanged(nameof(Written));
        OnPropertyChanged(nameof(Ruler));
        OnPropertyChanged(nameof(Explanation));
        OnPropertyChanged(nameof(ChordName));
    }

    partial void OnChosenChanged(CommandChoice value) => Moved();

    partial void OnTickChanged(int value) => Moved();

    partial void OnEveryChanged(double value) => Moved();

    partial void OnFallChanged(double value) => Moved();

    partial void OnUpChanged(double value) => Moved();

    partial void OnUp2Changed(double value) => Moved();

    partial void OnSlideChanged(double value) => Moved();

    partial void OnSkipChanged(double value) => Moved();

    partial void OnLevelChanged(double value) => Moved();

    partial void OnBeatsChanged(double value) => Moved();

    /// <summary>The slowest tempo a command can set, which is the slowest a song allows.</summary>
    public const int MinTempo = (int)TrackerTiming.MinBpm;

    partial void OnPlaceChanged(double value) => Moved();

    /// <summary>What a command does over one line on a note, worked out by the command itself.</summary>
    private IEnumerable<Tracker.Commands.Records.TickEvent> Spread(Tracker.Commands.Interfaces.ITickCommand command)
    {
        var into = new List<Tracker.Commands.Records.TickEvent>();
        var note = new Note(48);
        var cell = new TrackerEvent(0, 0, Tracker.Enums.TrackerEventKind.Trigger, note, 0, 1f, Command);

        var voice = new Tracker.Commands.VoiceState();
        voice.Struck(note);

        command.Spread(cell, voice, TicksPerLine, into);

        return into;
    }

    /// <summary>Two values of nought to fifteen as one byte, the first as the high digit.</summary>
    private static int Nibbles(double high, double low) =>
        (Math.Clamp((int)Math.Round(high), 0, 15) << 4) | Math.Clamp((int)Math.Round(low), 0, 15);

    /// <summary>A value held to nought and a ceiling, as a whole number.</summary>
    private static int Byte(double value, int most) => Math.Clamp((int)Math.Round(value), 0, most);

    /// <summary>A count of ticks in words.</summary>
    private static string Ticks(int count) => count == 1 ? "1 tick" : count + " ticks";

    /// <summary>How much of a line so many ticks are, in the words a musician would use.</summary>
    private string Share(int count)
    {
        if (count == 0) return "none";
        if (count * 4 == TicksPerLine) return "a quarter of a line";
        if (count * 3 == TicksPerLine) return "a third of a line";
        if (count * 2 == TicksPerLine) return "half a line";
        if (count * 3 == TicksPerLine * 2) return "two thirds of a line";
        if (count * 4 == TicksPerLine * 3) return "three quarters of a line";

        return count + " twelfths of a line";
    }

    /// <summary>What a retrigger will do, in words.</summary>
    private string RetriggerWords()
    {
        int hits = Spread(_retrigger).Count();
        int falls = (int)Math.Round(Fall);

        string loud = falls == 0
            ? "all at the same volume"
            : $"each {Math.Round(100 - falls / 16.0 * 100)}% as loud as the one before";

        return $"The note plays {hits} times in its line, every {Ticks((int)Math.Round(Every))}, {loud}.";
    }

    /// <summary>What an arpeggio will do, in words.</summary>
    private string ArpeggioWords()
    {
        int up = (int)Math.Round(Up);
        int up2 = (int)Math.Round(Up2);

        if (up == 0 && up2 == 0) return "Both intervals at nought: the note stays where it is.";

        string named = ChordName.Length > 0 ? " (" + ChordName.ToLowerInvariant() + ")" : "";

        return $"The note steps through itself, {up} semitones up and {up2} up{named}, one step a tick. It lasts one line: select the lines first to write it on all of them.";
    }

    /// <summary>How many ticks the glide takes, a whole line where it says nought.</summary>
    private int GlideTicks => Byte(Slide, 0xFF) == 0 ? TicksPerLine : Byte(Slide, 0xFF);

    /// <summary>What a glide will do, in words.</summary>
    private string GlideWords()
    {
        int ticks = GlideTicks;
        string length = ticks % TicksPerLine == 0
            ? (ticks / TicksPerLine == 1 ? "one line" : ticks / TicksPerLine + " lines")
            : Ticks(ticks) + (ticks < TicksPerLine ? ", " + Share(ticks) : ", " + (ticks / (double)TicksPerLine).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " lines");

        return $"The note already sounding slides to this cell's note over {length}, without starting again. With nothing sounding the note just starts.";
    }

    /// <summary>Where a pan puts the note, in words.</summary>
    private string PanWords()
    {
        int place = Byte(Place, TrackerCell.MaxVolume);
        int centre = TrackerCell.MaxVolume / 2;

        if (place == centre) return "This note sits in the centre.";

        int share = (int)Math.Round(Math.Abs(place - centre) * 100.0 / centre);

        return $"This note sits {share}% to the {(place < centre ? "left" : "right")}.";
    }
}
