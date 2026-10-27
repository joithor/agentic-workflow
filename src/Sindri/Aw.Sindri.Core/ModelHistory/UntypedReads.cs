using System.Text.Json;

namespace Aw.Sindri.Core.ModelHistory;

/// <summary>The two ledger fields the model-history fold reads as raw JSON until the typed records carry them, in one place.</summary>
internal static class UntypedReads
{
    // typed reads (LaunchStarted.Model, WorkflowEventDetail.FailureKind) once feat/gh-1206-m1b-events merges

    /// <summary>The model a 'launch.started' payload names ('model'), or null when it names none (a run without a model has no history row).</summary>
    /// <param name="payload">The payload's JSON text.</param>
    /// <returns>The model id, or null.</returns>
    internal static string? LaunchModel(string payload) => Text(payload, "model");

    /// <summary>The failure kind a 'step.failed' row's detail names ('detail.failureKind'), or null when it names none.</summary>
    /// <param name="payload">The workflow row's JSON text.</param>
    /// <returns>The kind as written, or null.</returns>
    internal static string? FailureKind(string payload) => Text(payload, "detail", "failureKind");

    /// <summary>
    /// The escalation a 'model.picked' payload records: the instance, state, the model that was escalated from ('escalatedFrom') and how it failed ('escalatedFromKind', 'refused' or
    /// 'check_failed'). Null when the pick is not an escalation or names no kind.
    /// </summary>
    /// <param name="payload">The payload's JSON text.</param>
    /// <returns>The escalation, or null.</returns>
    internal static EscalatedPick? Escalation(string payload) =>
        Text(payload, "instance") is { } instance && Text(payload, "state") is { } state && Text(payload, "escalatedFrom") is { } from
            && Text(payload, "escalatedFromKind") switch { "refused" => ModelOutcome.Refused, "check_failed" => ModelOutcome.CheckFailed, _ => (ModelOutcome?)null } is { } outcome
                ? new EscalatedPick(instance, state, from, outcome)
                : null;

    private static string? Text(string payload, params string[] path)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var node = document.RootElement;
            foreach (var name in path)
            {
                if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(name, out node))
                {
                    return null;
                }
            }

            return node is { ValueKind: JsonValueKind.String } && node.GetString() is { Length: > 0 } text ? text : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
