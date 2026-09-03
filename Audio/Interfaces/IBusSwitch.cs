namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Whether everything this application plays is summed onto one bus before it leaves.
/// </summary>
/// <remarks>
/// **Off until it has been listened to**, which is the rule this application keeps for every
/// change to the audio path, and the reason it is kept is written into the history: the last time
/// the summing was rearranged it arrived beside a mixer moved to a tab of its own, a wiring
/// graph, new pad voices and a buffer that stopped being a fixed sixty milliseconds. The sound
/// came apart, six things had moved, and the whole lot went back rather than the one that did it.
///
/// So it is one switch over one change. Off, every source reaches the card the way it always did
/// and not a line of the old path is different. On, the pads and the tracker are decoding
/// channels on one bus, which is the only arrangement an ASIO driver can carry.
///
/// Read from the environment rather than the settings, following <c>JB_REALTIME</c> and
/// <c>JB_PLUGINS_INPROCESS</c>: a switch that has to survive being read before the settings are
/// loaded, and be the same answer in a plugin's own process, has no business being a tick box
/// yet. It becomes one when it is the only path.
/// </remarks>
public interface IBusSwitch
{
    /// <summary>Whether the bus is asked for.</summary>
    bool Wanted { get; }
}
