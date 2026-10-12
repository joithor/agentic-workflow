using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>What follows a refusal (spec §5.5), in precedence order: budget park, escalation, re-run or rounds or needs-answer park, wait park, command refusal.</summary>
/// <param name="Follow">What follows.</param>
/// <param name="Round">The round a re-run starts.</param>
/// <param name="Suspend">The park or escalation.</param>
/// <param name="Error">SND-WF-004 for a refused command.</param>
public sealed record RefusalOutcome(RefusalFollow Follow, int? Round, Suspension? Suspend, Error? Error);
