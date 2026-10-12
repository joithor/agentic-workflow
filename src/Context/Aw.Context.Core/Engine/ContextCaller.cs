namespace Aw.Context.Core.Engine;

/// <summary>The caller of a facade call.</summary>
/// <param name="Kind">Human, session or service.</param>
/// <param name="Repo">The repo a session is scoped to (X-Sindri-Repo), or null.</param>
/// <param name="Client">The transport.</param>
public sealed record ContextCaller(ContextCallerKind Kind, string? Repo, ContextClient Client);
