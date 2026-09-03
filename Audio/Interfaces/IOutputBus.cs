using System;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Everything this application plays, summed into one stream.
/// </summary>
/// <remarks>
/// There are three things that make sound here and they used to reach the card separately: the
/// tracker's mix, the pads, and the take being auditioned on RECORD. The library summed them at
/// the device, which works for as long as there is a device to sum them at.
///
/// An ASIO driver is not one. The driver owns the card, so BASS is opened on its own silent
/// device and whatever the driver is pulling is the only thing anybody hears: the tracker, since
/// that is the one source that was ever handed over. Picking a driver silenced the pads and the
/// take preview, and said nothing, because from BASS's side every call still succeeded.
///
/// So the summing moves up here, where this application can see it. What is played, or handed to
/// a driver, is this one stream, and the three sources are decoding channels plugged into it.
/// That is the same arrangement whichever kind of output is picked, which is the point: there is
/// no second path left for a source to be missing from.
///
/// **It is not the song's master.** The tracks, the busses, the master strip and its effect chain
/// are <c>TrackMixer</c>'s and stay there. A pad is not on a track and never has been, so running
/// one through a song's master chain would change what a song sounds like when somebody hits
/// FIRE. This sums three things that were already finished, and has no settings of its own.
///
/// Two rules come from the add-on and are worth knowing before writing against this. A source has
/// to be a decoding channel, since the bus pulls it rather than being pushed to, and a channel
/// can be plugged into one bus only.
/// </remarks>
public interface IOutputBus : IDisposable
{
    /// <summary>Whether the add-on is here at all, asked once and remembered.</summary>
    /// <remarks>
    /// A missing native library throws on the first call into it rather than when the assembly
    /// loads, so the only honest way to find out is to ask it something and see. Where it is no,
    /// nothing here does anything and every source plays the way it always did.
    /// </remarks>
    bool Present { get; }

    /// <summary>
    /// Where this bus sits between the speakers, -1 hard left to 1 hard right.
    /// </summary>
    /// <remarks>
    /// Kept here as well as written into the channel, for the reason <see cref="Level"/> is: the
    /// stream is made again whenever the output changes and a setting that lived only in the
    /// channel would go with it.
    /// </remarks>
    double Pan { get; set; }

    /// <summary>
    /// Whether this bus is silenced, without disturbing where its fader stands.
    /// </summary>
    /// <remarks>
    /// Its own answer rather than a level of nought, because a mute has to be undone: turning the
    /// fader down to silence it would lose where it was, which is the whole difference between a
    /// mute and a fader. What reaches the channel is the two together.
    /// </remarks>
    bool Mute { get; set; }

    /// <summary>The stream everything is summed into, or nought before it is open.</summary>
    int Handle { get; }

    /// <summary>
    /// How much audio is held ahead of the card, in milliseconds, and nought to leave the
    /// library's own.
    /// </summary>
    /// <remarks>
    /// **This has to be set, and forgetting it is a fault that sounds like the music coming
    /// apart rather than like a buffer.** The bus is the channel that plays, so it is the channel
    /// that carries the buffer, and what used to carry it was the tracker's own stream at the
    /// 2048 frames the audio settings ask for, which is 46 ms at 44100. Left alone the bus takes
    /// the library's default of 500, so the mixing is pulled eleven times further ahead of real
    /// time than anything here was built for.
    ///
    /// That is not merely latency, because the sequencer's clock is wall time rather than the
    /// render. A note is fired when the clock says so and lands wherever the rendering has got
    /// to, so the distance between the two is what decides where a note falls in the music. Move
    /// it by half a second and a chord's notes stop landing together: from a chair it is the
    /// alignment of the tracks going, not a buffer being long.
    /// </remarks>
    int BufferMs { get; set; }

    /// <summary>
    /// What is leaving this bus, as two peaks from 0 to 1, and silence where it is not open.
    /// </summary>
    /// <remarks>
    /// **Which call this makes depends on where the bus stands, and the wrong one eats the
    /// audio.** A bus that plays itself can be measured the ordinary way, from its playback
    /// buffer. A bus that is a source on another one is a decoding channel, and there the
    /// ordinary call measures by decoding data out and throwing it away, so a meter would take
    /// blocks the mix never gets. That is not a theory: it is what the tracker's own meter did for
    /// an afternoon, and it presented as the whole song wandering out of time rather than as
    /// anything to do with a meter.
    /// </remarks>
    (float Left, float Right) Reading { get; }

    /// <summary>Whether the bus is open and sources can be plugged into it.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// How loud everything on this bus is, 0 to 1, and 1 until something says otherwise.
    /// </summary>
    /// <remarks>
    /// This is what makes a bus worth having one of per source rather than plugging everything
    /// into one. A bus is a strip: the pads' streams are summed on their own bus, that bus goes
    /// on the output as a single source, and one fader over it is the pads against the song
    /// however many pads are down. The same for the take being auditioned.
    ///
    /// Kept here as well as written into the channel, so the reading survives the bus being
    /// closed and opened again, which is what changing the output device does.
    /// </remarks>
    float Level { get; set; }

    /// <summary>
    /// Opens the bus at a rate and a width.
    /// </summary>
    /// <remarks>
    /// Whatever was open is closed first, since one of these is one output.
    ///
    /// A bus that is pulled is a decoding channel and one that plays itself is not, and that is
    /// the whole of what the output kind decides here. It is asked for before the stream is made
    /// rather than after, because a stream that plays itself and is also pulled is the same audio
    /// leaving by two routes.
    /// </remarks>
    /// <param name="rate">The rate to sum at, which is the rate the output is running at.</param>
    /// <param name="channels">How wide, which is two everywhere here.</param>
    /// <param name="pulled">True where a driver pulls the bus rather than the bus playing itself.</param>
    /// <returns>False where the add-on is missing or the stream would not open.</returns>
    bool Open(int rate, int channels, bool pulled);

    /// <summary>
    /// Plugs a source into the bus.
    /// </summary>
    /// <remarks>
    /// The source has to be a decoding channel. One that is not is refused by the add-on rather
    /// than mixed silently, and the refusal is written down: a source that quietly fails to join
    /// is a source nobody can hear with nothing anywhere saying why, which is the fault this whole
    /// class exists to end.
    ///
    /// A source already on the bus is left where it is rather than added twice.
    /// </remarks>
    /// <param name="source">The decoding channel to sum in.</param>
    /// <returns>False where there is no bus, or the add-on would not take it.</returns>
    bool Add(int source);

    /// <summary>
    /// Unplugs a source, and does nothing for one that is not on the bus.
    /// </summary>
    /// <param name="source">The channel that was plugged in.</param>
    void Remove(int source);

    /// <summary>
    /// Hears only these sources, and every one of them, or all of them where none is named.
    /// </summary>
    /// <remarks>
    /// **This is what a solo is, and the bus is the only place it can be done honestly.** Solo
    /// means only this, so it is a statement about every source at once rather than a flag on
    /// one, and the bus is the only thing that knows them all. Written as a mute on each of the
    /// others it would have to remember what each was before and would fight the mutes somebody
    /// set by hand.
    ///
    /// A source that is not heard is paused rather than turned down, which is the add-on's own
    /// <see cref="ManagedBass.BassFlags.MixerChanPause"/>: it stops being asked for audio at all,
    /// so it costs nothing while it is silent and its own level is left where it stands.
    ///
    /// The tracker needs no special case and is not named here. It is a source on this bus like
    /// the others, so soloing the pads pauses it along with everything else not named.
    /// </remarks>
    /// <param name="sources">What to hear, or nothing at all to hear everything.</param>
    void HearOnly(System.Collections.Generic.IReadOnlyCollection<int> sources);

    /// <summary>Whether a source is on the bus.</summary>
    /// <param name="source">The channel to ask about.</param>
    bool Holds(int source);

    /// <summary>Lets the bus go, and does nothing twice.</summary>
    /// <remarks>
    /// The sources are unplugged rather than freed. They belong to whoever made them, and a bus
    /// that freed them would leave the pads and the tracker holding handles to nothing.
    /// </remarks>
    void Close();
}
