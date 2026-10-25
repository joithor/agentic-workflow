using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>An issue the tracker contract seeds.</summary>
/// <param name="Key">The issue's key.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its description.</param>
/// <param name="Project">The name of the project it belongs to, or null.</param>
/// <param name="Comments">Its comments, in order.</param>
public sealed record TrackerSeedIssue(string Key, string Title, string Text, string? Project, IReadOnlyList<TrackerComment> Comments);
