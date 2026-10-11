namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// A park that arrived while the instance was already parked or taken over (spec §5.1): kept on the suspended frame
/// and shown by status. When the current frame pops, it takes effect instead of the resume action.
/// </summary>
/// <param name="Suspension">The park.</param>
/// <param name="Resume">Its resume action.</param>
public sealed record PendingPark(Suspension Suspension, ResumeAction Resume);
