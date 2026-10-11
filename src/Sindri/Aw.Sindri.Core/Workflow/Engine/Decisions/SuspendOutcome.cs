using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>A park or an escalation a rule decided (spec §3.8, §5.8): what suspends the frame, and what its answer will run.</summary>
/// <param name="Suspension">The park or escalation.</param>
/// <param name="Resume">The resume action the suspended frame stores.</param>
public sealed record SuspendOutcome(Suspension Suspension, ResumeAction Resume);
