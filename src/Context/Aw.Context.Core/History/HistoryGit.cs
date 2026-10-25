using Aw.Context.Core.Engine;
namespace Aw.Context.Core.History;

/// <summary>What the git step read in one repo.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The checkout's HEAD, abbreviated.</param>
/// <param name="UpTo">The map version's commit that bounded the read, or null when HEAD did.</param>
/// <param name="Shallow">True when the clone is shallow (older history may be missing).</param>
/// <param name="Files">The files searched in this repo.</param>
/// <param name="Changes">The changes found in this repo.</param>
public sealed record HistoryGit(ContextRepoName Repo, string Head, string? UpTo, bool Shallow, int Files, int Changes);
