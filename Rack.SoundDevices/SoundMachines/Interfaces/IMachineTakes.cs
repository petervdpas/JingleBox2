
namespace JingleBox2.Rack.SoundDevices.SoundMachines.Interfaces;

/// <summary>
/// Where a panel goes to find out about the recordings a machine names.
/// </summary>
/// <remarks>
/// A text setting holding a take is a name and nothing else, and a name draws badly: it is
/// usually a file, often a long one, and it says nothing about how long the sound is or what
/// shape it has. The two things a panel wants are the picture and the wording, so those are the
/// two things asked for here.
///
/// Deliberately small, and deliberately not a recording. The panel is given no way to load,
/// play or edit a take, because whoever supplies this owns the shelf the takes are kept on and
/// the panel is only drawing what is on it. A take this cannot place is not an error either:
/// there is no picture and the wording is whatever can be said about a name, which is what a
/// panel showing a recording that has since been thrown away should look like.
/// </remarks>
public interface IMachineTakes
{
    /// <summary>The shape of that recording, or nothing when there is none to draw.</summary>
    float[]? Peaks(string take);

    /// <summary>What to write on a control standing for that recording.</summary>
    string Describe(string take);
}
