using Aw.Context.Core.Engine;

namespace Aw.Context.Features.QueryContext;

/// <summary>A question and the scope it is asked in.</summary>
/// <param name="Query">The question.</param>
/// <param name="Scope">The profile's settings and repos, and the caller.</param>
internal sealed record QueryContextRequest(ContextQuery Query, ContextScope Scope);
