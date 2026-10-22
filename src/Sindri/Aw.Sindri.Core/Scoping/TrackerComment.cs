namespace Aw.Sindri.Core.Scoping;

/// <summary>A comment on a tracker record, in the tracker's order.</summary>
/// <param name="Author">Who wrote it.</param>
/// <param name="Text">What it says: untrusted text.</param>
public sealed record TrackerComment(string Author, string Text);
