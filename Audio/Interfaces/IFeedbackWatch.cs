namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Listens to what is being monitored and says when it has started to ring.
/// </summary>
/// <remarks>
/// **Acoustic feedback, which is the one loop no routing can see.** A signal that leaves the
/// machine through a speaker and comes back through a microphone is not on any graph and no
/// subsystem can be asked about it: the only evidence is in the audio. Which is why this reads a
/// block and nothing else, and why it is not beside
/// <c>JingleBox2.Audio.Routing.Interfaces.IAudioRouting.IsOurOutput</c>, which answers the
/// digital half of the same question from the wiring.
///
/// **The other half of <see cref="IOutputCurve"/>.** That is the instant: the last thing a sample
/// goes through, bending what is too loud and silencing what is not a number. It sees one sample
/// and can never see a trend. A ring is not too loud yet and is getting there, which is a fact
/// about a run of blocks.
///
/// **Only ever on the path where a signal can come back**, which here is the recorder being heard
/// through the desk. Deliberately not over the mix: a delay at the top of its feedback knob and a
/// filter self-oscillating are somebody's sound rather than a fault, and a watchdog over the
/// master would have made the whole of that a bug. The rule is the path and not the phenomenon.
///
/// **What it is for is somebody who does not yet know what a monitor path is**, which is who this
/// happens to. So it is on with nothing to switch, and whoever acts on it is expected to say what
/// to do about it rather than name it.
/// </remarks>
public interface IFeedbackWatch
{
    /// <summary>
    /// Hands it the next block, and says whether it has become sure.
    /// </summary>
    /// <remarks>
    /// **Two tests every frame rather than a fast one and a slow one**, because they answer
    /// different halves and either alone is wrong. A ring collapses towards a sine, so the crest
    /// falls to near a sine's own; and it is one narrow frequency, well above its neighbours and
    /// the rest of the spectrum, holding still and growing. Speech and music fail the first, a
    /// held note fails the growth, and a played note of the same pitch fails the narrowness, since
    /// it brings a harmonic series with it.
    ///
    /// **Growth and not merely persistence is what keeps a test tone out of it.** A sine played
    /// into a microphone on purpose is narrow, is tonal and does not go away, and the one thing it
    /// does not do is climb. Asking for a rise across the window is the whole of the difference,
    /// and it is also what makes this an onset detector: a ring that has already reached the top
    /// and levelled off is not climbing either. That is the right way round, since the point is to
    /// catch it on the way up.
    ///
    /// True is said once and then the run is forgotten, so a caller that keeps feeding blocks does
    /// not hear it over and over. <see cref="Clear"/> is what arms it again.
    ///
    /// Called on the thread the capture arrives on. Nothing here allocates after the first block
    /// and nothing waits, which is the same standing the clipping test on that thread already has.
    /// </remarks>
    /// <param name="block">Interleaved stereo samples, as the monitor is about to play them.</param>
    /// <param name="floats">How many of them are really there.</param>
    /// <param name="rate">
    /// What the audio is running at, so the bounds can be in hertz rather than in bins. Handed in
    /// per block rather than held, since it is the caller that knows when it changes and there is
    /// nothing here worth keeping in step with it.
    /// </param>
    /// <returns>True the moment it is sure, once per run.</returns>
    bool Ringing(float[] block, int floats, int rate);

    /// <summary>Forgets everything, which is what arms it for a fresh run.</summary>
    /// <remarks>
    /// Said when listening is switched on. Without it the frames either side of a gap would be
    /// read as one run, and a level that happened to be climbing when somebody stopped listening
    /// would still be climbing when they started again an hour later.
    /// </remarks>
    void Clear();
}
