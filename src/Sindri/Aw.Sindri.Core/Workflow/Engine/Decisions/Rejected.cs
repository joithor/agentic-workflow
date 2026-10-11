using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// A validation refusal (spec W12, §5.3 steps 3–7): a stale run token (SND-WF-005), a trigger not allowed here
/// (SND-WF-003), a malformed report (SND-WF-008), a human trigger on a revoked or drifted recipe (SND-RECIPE-005/006),
/// or an answer that doesn't fit (SND-WF-003). It writes nothing and consumes nothing.
/// </summary>
/// <param name="Key">The trigger's key.</param>
/// <param name="Error">The error, with its catalogue code and fix.</param>
public sealed record Rejected(TriggerKey Key, Error Error) : Decision(Key);
