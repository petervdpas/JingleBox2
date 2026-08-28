using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace JingleBox2.Models;

/// <summary>
/// One take on the shelf: a file this application owns, and what is known about it.
/// </summary>
/// <remarks>
/// The shelf is where every recording in this program comes from. A pad plays a path underneath
/// and an instrument points at a file, but both of them pick from here, which is what makes the
/// application the owner of every file anything depends on.
///
/// Observable throughout, because a take is edited while it is on screen: trimming changes its
/// length, filing it changes its category, and the list has to follow both.
/// </remarks>
public sealed partial class Recording : ObservableObject
{
    /// <summary>What this take is called in a config file or a song, rather than by its path.</summary>
    /// <remarks>
    /// Its own identity rather than the file name, so a take can be renamed without everything
    /// that plays it having to be found and corrected.
    /// </remarks>
    public required string Id { get; set; }

    /// <summary>Where the file is now.</summary>
    /// <remarks>
    /// Written down portably where it lives in the application folder, so a song survives that
    /// folder moving or being on another machine.
    /// </remarks>
    public required string FilePath { get; set; }

    /// <summary>When it was recorded, which is what the shelf is sorted by.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Whatever somebody wrote about it, and empty for almost all of them.</summary>
    public string Description { get; set; } = "";

    /// <summary>What it is called, which is a person's word and not the file's.</summary>
    [ObservableProperty] private string name = "";

    /// <summary>How long it is, in milliseconds. Changes under the list when a take is trimmed.</summary>
    /// <remarks>
    /// Observable because it moves while the list is on screen: trimming a recording changes its
    /// duration, and the row showing it has to follow.
    /// </remarks>
    [ObservableProperty] private long durationMs;

    /// <summary>
    /// The instruments that play this recording, as a phrase, or empty when nothing does. A
    /// sample instrument points at the file itself, so one that is spoken for cannot be
    /// deleted without silencing every song that uses it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInUse))]
    private string usedBy = "";

    /// <summary>True when something plays this, which is what stops it being deleted quietly.</summary>
    public bool IsInUse => !string.IsNullOrEmpty(UsedBy);

    /// <summary>
    /// What this take is filed under, or empty when it is filed under nothing.
    /// </summary>
    /// <remarks>
    /// Kept beside the takes rather than in the file name, so a category can be changed
    /// without the take becoming a different file. <see cref="Audio.RecordingCategories"/> is
    /// where it is written down.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCategory))]
    private string category = "";

    /// <summary>True when it is filed under something, for a list that shows the label.</summary>
    public bool HasCategory => Category.Length > 0;

    /// <summary>True while this recording is the one being auditioned from the list.</summary>
    [ObservableProperty] private bool isPlaying;

    /// <summary>
    /// What a picker shows when it is given one of these and told nothing else.
    /// </summary>
    /// <remarks>
    /// Every other thing in this app that lands in a list says its own name this way, and this
    /// was the one that did not, so a take picker offered four rows all reading
    /// "JingleBox2.Models.Recording".
    /// </remarks>
    public override string ToString() => Name;
}

/// <summary>
/// A recording's shape, worked out once so a picture of it can be drawn many times.
/// </summary>
/// <remarks>
/// The peaks and not the audio: decoding a take is seconds and there is no reason to do it again
/// every time a window is resized or a loop point is dragged.
/// </remarks>
public class WaveformData
{
    /// <summary>The peak of each slice of the recording, in the order they play.</summary>
    /// <remarks>
    /// How many slices there are is whatever the reader chose, so a position on the picture is
    /// worked out from the length rather than from a fixed rate.
    /// </remarks>
    public required float[] PeakData { get; set; }

    /// <summary>The recording's own rate, which is what turns a sample number into a time.</summary>
    public int SampleRate { get; set; }

    /// <summary>How many channels it has.</summary>
    public int Channels { get; set; }

    /// <summary>Number of sample frames, so this is the length in samples per channel.</summary>
    public long TotalSamples { get; set; }
}

/// <summary>
/// The part of a recording somebody kept, in sample frames.
/// </summary>
/// <remarks>
/// Frames rather than seconds, because this is compared with and turned into positions in the
/// file, and seconds would put a rounding error between the picture and the sound.
/// </remarks>
public class TrimRegion
{
    /// <summary>Where it starts.</summary>
    public long StartSample { get; set; }

    /// <summary>And where it ends. The same as the start means nothing was trimmed.</summary>
    public long EndSample { get; set; }
}
