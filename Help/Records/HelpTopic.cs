namespace JingleBox2.Help.Records;

/// <summary>
/// One thing the app can explain about itself: a short line for a tooltip and the full text
/// for the help window.
/// </summary>
public sealed record HelpTopic(string Id, string Title, string Summary, string Body);
