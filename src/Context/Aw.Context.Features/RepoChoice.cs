using Aw.Context.Core.Engine;

namespace Aw.Context.Features;

/// <summary>The repo an answer is for and the cut of the graph to it.</summary>
/// <param name="Name">The repo's name, or null when the profile has several and none was named.</param>
/// <param name="Cut">The repo to cut the graph to, or null for the whole graph.</param>
internal readonly record struct RepoChoice(string? Name, ContextRepoName? Cut);
