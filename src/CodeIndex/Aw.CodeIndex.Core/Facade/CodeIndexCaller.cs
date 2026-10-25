using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>Who asks. Sindri's handlers enforce a session's scope (the same not-found for a repo outside it); the facade carries the caller so it can be audited.</summary>
/// <param name="Kind">Human, service or session.</param>
/// <param name="Repo">The repo a session is scoped to (X-Sindri-Repo), or null.</param>
public sealed record CodeIndexCaller(CallerKind Kind, RepoName? Repo);
