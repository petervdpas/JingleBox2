namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// How finely this process is allowed to measure a wait.
/// </summary>
/// <remarks>
/// Windows runs its scheduler on a tick, and a process that has not asked for better gets the
/// default one, which is 15.625 ms. Every wait in the program is then rounded up to the next
/// tick, whether it is spelled as a sleep, as a wait on a handle, or as a timer on the thread
/// things are drawn on. Nothing reports this: the waits simply take longer than they say, and
/// what somebody sees is an application that feels slow for no reason anything in it can
/// account for.
///
/// Measured on one machine, before and after asking:
///
/// | | default | asked |
/// |---|---|---|
/// | a 1 ms sleep | 15.21 ms | 1.76 ms |
/// | a 16 ms sleep | 30.00 ms | 16.69 ms |
/// | a 10 ms wait | 15.19 ms | 10.44 ms |
/// | a 48 ms wait | 61.21 ms | 48.54 ms |
///
/// What that costs in this program: the loop that pumps a plugin's own window asks for 16 ms
/// and gets 30, so somebody else's interface is drawn at half the rate it was written for; the
/// tracker's clock spins out the last two milliseconds of every step and can be handed back as
/// much as fifteen; and every meter on every page runs at 61 ms rather than 50.
///
/// Linux has no such tick and needs nothing, which is why this was never noticed there and why
/// asking answers false rather than throwing.
/// </remarks>
public interface IClockResolution
{
    /// <summary>Asks for the finest tick the system will give, and says whether it was given.</summary>
    /// <remarks>
    /// Held for the life of the process rather than taken and given back around each wait.
    /// Since Windows 10 the tick is per process, so this slows nothing else down, and a program
    /// whose whole job is audio and drawing wants it throughout rather than in patches.
    ///
    /// Asked once, at the top of the process, before anything has made a thread that will wait.
    /// Both processes this executable runs as ask: a plugin's own host has the loop that suffers
    /// worst from it and reads no settings at all, so it cannot be told and has to ask for
    /// itself.
    ///
    /// False where there is nothing to ask, which is every system but Windows, and false where
    /// the system refused. Neither is worth stopping for: the program is correct either way and
    /// only slower.
    /// </remarks>
    bool Take();

    /// <summary>What was arranged, in words fit for a log.</summary>
    string Said();
}
