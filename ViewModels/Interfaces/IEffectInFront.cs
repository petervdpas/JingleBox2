namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// Which of our effects has its face in front of you, when one is open in a window.
/// </summary>
/// <remarks>
/// What a link pointed at one of ours resolves against, before anything else. Until this there
/// was one answer, the chain of the track you are working on, and that is right while you are
/// working in the pattern and wrong in three separate ways the moment a face is open in a window,
/// which is exactly when a hand is reaching for a knob.
///
/// A track's chain follows the cursor, so a face left open while an instrument window claims a
/// different track resolved against that other track. The master's chain is on the mixer and
/// follows nothing, so it never matched the cursor at all. And a pad's chain is not on a track in
/// the first place, so no answer phrased as a track number could ever have reached it: a knob
/// pointed at an effect on a pad moved nothing, ever.
///
/// Nothing is applied by saying this. The mappings are walked per message, so the next thing you
/// touch simply resolves somewhere else, which is the same rule the instrument window's own
/// <see cref="ITrackerPanel.PanelInFront"/> keeps about tracks.
/// </remarks>
public interface IEffectInFront
{
    /// <summary>The box whose face is in front, or nothing when none is open.</summary>
    IEffectShown? Shown { get; }

    /// <summary>A face was opened or brought forward, so its box is the one being worked on.</summary>
    /// <param name="shown">The box. Nothing says the same as no face at all.</param>
    void InFront(IEffectShown? shown);

    /// <summary>
    /// And that face has gone.
    /// </summary>
    /// <remarks>
    /// Only when the one that left is the one that was in front. Closing the window behind the
    /// one you are using is not you leaving the one you are using, which is the rule the
    /// instrument window already keeps for tracks.
    /// </remarks>
    /// <param name="shown">The box whose face closed.</param>
    void Gone(IEffectShown? shown);
}
