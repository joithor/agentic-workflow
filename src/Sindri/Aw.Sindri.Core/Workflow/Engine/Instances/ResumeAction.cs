using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A suspended frame's resume action (spec W16, §5.8): what the answer that pops the frame runs.</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Withheld">The withheld trigger, for transition, reevaluate, override and a drift rearm parked by a Step trigger.</param>
/// <param name="Gate">The escalated gate that '--accept' overrides.</param>
/// <param name="Effect">The refused outbox command that 'reemit' re-sends.</param>
public sealed record ResumeAction(ResumeKind Kind, WithheldTrigger? Withheld, BlockId? Gate, OutboxCommandId? Effect);
