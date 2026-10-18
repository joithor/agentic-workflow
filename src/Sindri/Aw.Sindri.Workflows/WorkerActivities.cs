namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The activity classes the worker runs.</summary>
/// <param name="Steps">The RunStep and LoadRecipe activities.</param>
/// <param name="Events">The RecordEvent activity (the interpreter runs it as a local activity).</param>
/// <param name="Items">The ReadItemRepo activity (a local activity on start).</param>
/// <param name="Effects">The effect activities: RecordEffect, RecheckEffect, RunEffect (engine P6).</param>
/// <param name="Inputs">The Step input activity: FillInput.</param>
internal sealed record WorkerActivities(RunStepActivities Steps, RecordEventActivities Events, FactReadActivities Items, EffectActivities Effects, StepInputActivities Inputs);
