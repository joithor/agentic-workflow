using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>What a loop says about entering its state again (spec §3.7): go on with the next round, or park with rounds or no-progress (kind loop).</summary>
/// <param name="Next">The loop's counters for the round it enters; null when it parks.</param>
/// <param name="Park">The park; null when it goes on.</param>
public sealed record LoopVerdict(LoopState? Next, SuspendOutcome? Park);
