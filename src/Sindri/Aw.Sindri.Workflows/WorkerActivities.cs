namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Every activity class the worker registers.</summary>
/// <param name="Steps">'LoadRecipe', 'RunStep' and 'RunBlockStep'.</param>
/// <param name="Events">'RecordEvent'.</param>
/// <param name="Items">'ReadItemRepo' and 'ReadSubject'.</param>
/// <param name="Effects">The effect activities.</param>
/// <param name="Agents">The agent runtime's activities (engine P8).</param>
internal sealed record WorkerActivities(RunStepActivities Steps, RecordEventActivities Events, FactReadActivities Items, EffectActivities Effects, AgentStepActivities Agents);
