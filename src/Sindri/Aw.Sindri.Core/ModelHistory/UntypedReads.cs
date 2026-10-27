namespace Aw.Sindri.Core.ModelHistory;

/// <summary>The two ledger fields the model-history fold reads as raw JSON until the typed records carry them, in one place.</summary>
internal static class UntypedReads
{
    // typed reads (LaunchStarted.Model, WorkflowEventDetail.FailureKind) once feat/gh-1206-m1b-events merges

    /// <summary>The model a 'launch.started' payload names ('model'), or null when it names none (a run without a model has no history row).</summary>
    /// <param name="payload">The payload's JSON text.</param>
    /// <returns>The model id, or null.</returns>
    internal static string? LaunchModel(string payload) => throw new NotSupportedException();

    /// <summary>The failure kind a 'step.failed' row's detail names ('detail.failureKind'), or null when it names none.</summary>
    /// <param name="payload">The workflow row's JSON text.</param>
    /// <returns>The kind as written, or null.</returns>
    internal static string? FailureKind(string payload) => throw new NotSupportedException();
}
