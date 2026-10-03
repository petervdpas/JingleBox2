using System;
using System.Collections.Generic;
using JingleBox2.Config.Enums;
using JingleBox2.Config.Records;

namespace JingleBox2.Config.Interfaces;

/// <summary>
/// Which parts of the tracker and the mixer are switched on, which is the one answer the screens
/// and the work behind them both ask.
/// </summary>
/// <remarks>
/// Off means the part is not drawn and its work is not done, both, since a part hidden on the
/// screen and still working behind it, or working on the screen and not behind it, is a program
/// telling somebody one thing and doing another. So nothing keeps a copy of a switch: the lane
/// player, the recorder, the mix and the pictures all ask here at the moment they need to know.
///
/// It exists for a machine with little to spare, such as a Raspberry Pi, where the work a part
/// does is worth more than the part. Everything is on unless somebody says otherwise. Read from
/// whichever thread needs it, the clock's and the audio path's included, and written from the
/// drawing thread.
/// </remarks>
public interface IFeatures
{
    /// <summary>Every switchable part, in the order the settings list them.</summary>
    IReadOnlyList<FeatureInfo> All { get; }

    /// <summary>The parts on one page, in the order the settings list them.</summary>
    /// <param name="page">The page.</param>
    IReadOnlyList<FeatureInfo> On(FeaturePage page);

    /// <summary>Whether a part is switched on. A part this build does not have is not.</summary>
    /// <param name="feature">The part.</param>
    bool IsOn(Feature feature);

    /// <summary>
    /// Switches a part on or off, writes it into the settings and says so. Setting what it
    /// already is does nothing and says nothing.
    /// </summary>
    /// <param name="feature">The part.</param>
    /// <param name="on">Whether it is to be on.</param>
    void Set(Feature feature, bool on);

    /// <summary>Raised on the thread that switched a part, after it was switched.</summary>
    event Action<Feature>? Changed;

    /// <summary>Whether the automation on one timeline is on: the song's, or a pattern's.</summary>
    /// <param name="songWide">True for a lane on the song's timeline, false for one on a pattern's.</param>
    bool Automates(bool songWide) => IsOn(songWide ? Feature.SongAutomation : Feature.PatternAutomation);
}
