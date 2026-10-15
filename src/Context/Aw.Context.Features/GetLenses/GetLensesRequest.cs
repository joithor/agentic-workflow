using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetLenses;

/// <summary>The intent, product and code bands around one node.</summary>
internal sealed record GetLensesRequest(string Anchor, bool Draft, ContextScope Scope);
