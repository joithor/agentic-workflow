namespace Aw.Sindri.Core.Scoping;

/// <summary>A child issue of a tracker project.</summary>
/// <param name="Key">The issue's key.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its description: untrusted text.</param>
/// <param name="Comments">Its first comments, at most the read's cap.</param>
public sealed record TrackerItem(string Key, string Title, string Text, IReadOnlyList<TrackerComment> Comments);
