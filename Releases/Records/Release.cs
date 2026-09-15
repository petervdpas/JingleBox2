namespace JingleBox2.Releases.Records;

/// <summary>One release of the application, as the place releases are published says it.</summary>
/// <param name="Tag">The tag it was cut from, such as <c>v2.5.4</c>.</param>
/// <param name="Page">The page it can be downloaded from.</param>
public sealed record Release(string Tag, string Page);
