using JingleBox2.Config.Enums;

namespace JingleBox2.Config.Records;

/// <summary>One switchable part: what it is, where it is, and how it is spoken of.</summary>
/// <param name="Feature">The part.</param>
/// <param name="Page">The page it is on.</param>
/// <param name="Word">How the settings file names it, which never changes.</param>
/// <param name="Name">The line on its switch.</param>
/// <param name="Hint">What switching it off does, said under the switch.</param>
public sealed record FeatureInfo(Feature Feature, FeaturePage Page, string Word, string Name, string Hint);
