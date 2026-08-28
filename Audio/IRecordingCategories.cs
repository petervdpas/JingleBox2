namespace JingleBox2.Audio;


/// <summary>
/// What each take is filed under, kept in one file beside the takes.
/// </summary>
/// <remarks>
/// A note about a recording rather than a folder to put it in, because the recording itself is
/// a path somebody wrote down: an instrument plays that file, a pad fires it, a song names it.
/// Filing a take into a folder would move it out from under all three, and out from under the
/// pad profiles that are not even loaded. A line in a file moves nothing.
///
/// It lives in the recordings folder, so copying the takes somewhere copies how they were
/// sorted with them. Lose the file and what is lost is the sorting, not a second of audio.
///
/// Keyed by name, which for a recording is its file name. A take renamed on this page is
/// followed; one renamed behind the app's back loses its category rather than inheriting
/// somebody else's.
/// </remarks>
public interface IRecordingCategories
{
    /// <summary>What that take is filed under, or empty when it is filed under nothing.</summary>
    /// <param name="name">The take's file name, which is the name a recording goes by here.</param>
    string Of(string name);

    /// <summary>Files a take, or takes it out of its category when given nothing.</summary>
    /// <param name="name">The take's file name.</param>
    /// <param name="category">What to file it under, or null or blank to file it under nothing.</param>
    void Put(string name, string? category);

    /// <summary>The take is called something else now, and keeps what it was filed under.</summary>
    /// <param name="from">What it was called.</param>
    /// <param name="to">What it is called now.</param>
    void Renamed(string from, string to);

    /// <summary>The take is gone, so the line about it is too.</summary>
    /// <param name="name">The take's file name.</param>
    void Forget(string name);
}
