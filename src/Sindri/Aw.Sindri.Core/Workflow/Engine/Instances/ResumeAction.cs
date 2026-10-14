using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A suspension's resume action (spec §5.8): what the answer that ends it runs.</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Withheld">The withheld trigger, for transition, reevaluate, override and a drift rearm parked by a Step trigger.</param>
/// <param name="Gate">The escalated gate that '--accept' overrides.</param>
/// <param name="Effect">The refused effect that 'reemit' runs again under its identity (§5.7), and that a drift approval's resume re-sends (P2).</param>
public sealed record ResumeAction(ResumeKind Kind, WithheldTrigger? Withheld, BlockId? Gate, RefusedEffect? Effect);
