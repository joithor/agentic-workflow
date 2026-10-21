namespace Aw.CodeIndex.Core.Facade;

/// <summary>The answer to Status: one row per repo in the caller's scope.</summary>
/// <param name="Repos">The repos' indexes.</param>
public sealed record StatusAnswer(IReadOnlyList<RepoStatus> Repos);
