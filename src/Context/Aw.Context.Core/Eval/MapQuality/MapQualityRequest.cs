using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Eval.MapQuality;

/// <summary>Measures the current map against the owner's decisions: a human or service call.</summary>
/// <param name="Scope">The caller and the approved section.</param>
public sealed record MapQualityRequest(ContextScope Scope);
