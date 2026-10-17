using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Eval.MapQuality;

/// <summary>Deletes eval files past 90 days, or all of them.</summary>
/// <param name="Scope">The caller (a session is refused).</param>
/// <param name="All">True to delete every eval file, the probe baselines included.</param>
public sealed record EvalPurgeRequest(ContextScope Scope, bool All);
