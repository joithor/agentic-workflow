using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The open question of a park, escalation or takeover (engine spec rev 8 §5.8, §6.4 'next' and 'answer').</summary>
/// <param name="Kind">Park, escalation or takeover.</param>
/// <param name="Reason">A park's reason.</param>
/// <param name="Question">The question or detail the human reads, scrubbed.</param>
public sealed record OpenQuestion(SuspensionKind Kind, ParkReason? Reason, string Question);
