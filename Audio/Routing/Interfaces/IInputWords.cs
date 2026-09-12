using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing.Interfaces;

/// <summary>
/// What the input channel says about itself, in one sentence for the status line.
/// </summary>
/// <remarks>
/// **One gesture is one sentence, and it used to be five writers racing.** Choosing a source or
/// throwing Hear it ran through <c>Listening</c>, <c>ApplyRoute</c> twice, <c>Agree</c> and the
/// switch's own setter, each writing the status line, and the last one to run won. Which one that
/// was depended on when a thread came back: the sentence explaining why nothing could be heard was
/// written and thrown away a millisecond later, every time, and the one saying a machine could not
/// move a source was never seen at all.
///
/// So the words live here and nowhere else. Nothing above this writes a status line about the
/// input, and this writes nothing else: it is handed what happened and answers what to say.
///
/// **Pure, and that is the whole of why it is separate.** No audio, no graph, no window and no
/// clock, so every sentence the input channel can produce can be read in a test, including the
/// ones that only happen when a machine refuses something, which is exactly the wording nobody
/// ever sees while writing it.
/// </remarks>
public interface IInputWords
{
    /// <summary>
    /// The one sentence for what the input channel has just been made to do.
    /// </summary>
    /// <remarks>
    /// Ordered by what somebody most needs to know, which is not the order things happened in. A
    /// source that gave nothing back is first, since nothing else about it matters yet; then the
    /// loop, since that is the one case where the switch is on and there is deliberately no sound;
    /// then a machine that could not move the source; and the ordinary outcome last.
    /// </remarks>
    /// <param name="source">What the input is pointed at, or nothing.</param>
    /// <param name="heard">Whether Hear it is on.</param>
    /// <param name="canHear">Whether it could be heard at all, or is this application's own output.</param>
    /// <param name="aside">What became of taking it off its own output.</param>
    /// <param name="connected">Whether the source is actually giving anything.</param>
    /// <returns>The sentence, or empty where there is nothing worth saying.</returns>
    string Line(AudioRoute? source, bool heard, bool canHear, InputAside aside, bool connected);
}
