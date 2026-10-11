namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A frame on the stack (spec §5.1, W16): kept exactly as it was, with what suspended it and what its answer runs.</summary>
/// <param name="Frame">The suspended frame (entry, run, child, deadlines as the suspension left them).</param>
/// <param name="Suspension">What suspended it.</param>
/// <param name="Resume">What the answer that pops it runs.</param>
/// <param name="Pending">A park that arrived meanwhile, which replaces the resume action on pop.</param>
public sealed record SuspendedFrame(Frame Frame, Suspension Suspension, ResumeAction Resume, PendingPark? Pending);
