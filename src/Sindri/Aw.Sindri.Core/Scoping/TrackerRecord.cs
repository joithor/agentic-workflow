namespace Aw.Sindri.Core.Scoping;

/// <summary>A tracker project or issue as the scoping station reads it (scoping spec §4, C8).</summary>
/// <param name="Key">The key as the tracker names it.</param>
/// <param name="Kind">Whether it is an issue or a project.</param>
/// <param name="Title">Its title or name.</param>
/// <param name="Text">Its description: untrusted text.</param>
/// <param name="Version">When the tracker last changed it (the brief reference's version).</param>
/// <param name="Comments">Its first comments, at most the read's cap.</param>
/// <param name="Children">A project's first issues, at most the read's cap.</param>
/// <param name="Truncated">Whether a cap cut children or comments.</param>
public sealed record TrackerRecord(string Key, TrackerKind Kind, string Title, string Text, string Version, IReadOnlyList<TrackerComment> Comments, IReadOnlyList<TrackerItem> Children, bool Truncated);
