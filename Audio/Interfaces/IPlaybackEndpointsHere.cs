namespace JingleBox2.Audio.Interfaces;

/// <summary>What this machine can be asked about its outputs with, if anything.</summary>
/// <remarks>
/// The one place that asks the machine what it is, the same shape <c>IAudioCapture</c> keeps and
/// for the same reason: everything else holds an <see cref="IPlaybackEndpoints"/> and never
/// learns which of the two it has.
/// </remarks>
public interface IPlaybackEndpointsHere
{
    /// <summary>What to read the outputs with here, which may be the one that says nothing.</summary>
    IPlaybackEndpoints Here();
}
