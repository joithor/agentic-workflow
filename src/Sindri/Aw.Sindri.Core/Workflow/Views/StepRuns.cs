namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Folds an instance's ledger events into its Step runs.</summary>
public static class StepRuns
{
    /// <summary>The runs <paramref name="events"/> (the instance's, in ledger order) open and close.</summary>
    /// <param name="events">The instance's events.</param>
    /// <param name="states">The instance's states, whose round caps give each run's max attempts.</param>
    /// <returns>One run per Step run, in start order.</returns>
    public static IReadOnlyList<StepRun> Fold(IEnumerable<RecordedWorkflowEvent> events, IReadOnlyList<StateCell> states) =>
        throw new NotSupportedException();
}
