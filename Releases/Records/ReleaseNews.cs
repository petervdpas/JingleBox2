using JingleBox2.UI.Enums;

namespace JingleBox2.Releases.Records;

/// <summary>What is worth telling somebody about the release they are running.</summary>
/// <param name="Text">The sentence.</param>
/// <param name="Kind">How it wants to be read.</param>
/// <param name="Link">The release's page, which the toast opens when clicked.</param>
public sealed record ReleaseNews(string Text, StatusKind Kind, string Link);
