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
