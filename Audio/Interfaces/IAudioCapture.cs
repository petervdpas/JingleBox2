namespace JingleBox2.Audio.Interfaces;

/// <summary>What this machine can listen to one program with, if anything.</summary>
/// <remarks>
/// The one place that asks the machine what it is. Everything else holds an
/// <see cref="IProgramCapture"/> and never learns which of the two it has, which is the rule
/// this codebase keeps at every seam where the platforms differ: the behaviour is decided first
/// and each machine is asked how it produces it.
/// </remarks>
public interface IAudioCapture
{
    /// <summary>What to listen to a program with here, which may be the one that says no.</summary>
    IProgramCapture Programs();
}
