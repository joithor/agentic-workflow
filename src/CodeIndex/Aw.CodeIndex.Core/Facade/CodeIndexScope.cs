namespace Aw.CodeIndex.Core.Facade;

/// <summary>What a facade call runs against: the repos the caller may see, each with its effective settings, and the caller. A repo outside it, or no such repo, answers the same SND-CODEIDX-013/011 not-found as a repo with no index.</summary>
/// <param name="Repos">The caller's repos.</param>
/// <param name="Caller">The caller.</param>
public sealed record CodeIndexScope(IReadOnlyList<CodeIndexRepo> Repos, CodeIndexCaller Caller);
