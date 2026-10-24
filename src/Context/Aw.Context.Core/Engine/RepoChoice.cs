namespace Aw.Context.Core.Engine;

/// <summary>The repo an answer is for, and the cut that scopes the graph to it (null when the graph is read whole).</summary>
/// <param name="Name">The repo's name, or null.</param>
/// <param name="Cut">The repo to cut the graph to, or null.</param>
public readonly record struct RepoChoice(string? Name, ContextRepoName? Cut);
