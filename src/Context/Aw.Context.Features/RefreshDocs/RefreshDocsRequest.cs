using Aw.Context.Core.Engine;

namespace Aw.Context.Features.RefreshDocs;

/// <summary>Asks to bring the repo-docs index up to date in a scope.</summary>
/// <param name="Scope">The profile's settings and repos, and the caller.</param>
internal sealed record RefreshDocsRequest(ContextScope Scope);
