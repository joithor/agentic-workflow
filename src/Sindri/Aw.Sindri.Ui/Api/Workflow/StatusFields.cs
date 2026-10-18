namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The fields of 'workflow status' (also current.status of an instance).</summary>
/// <param name="Header">Header, as served.</param>
/// <param name="Next">Next, as served.</param>
/// <param name="Answer">Answer, as served.</param>
/// <param name="Child">Child, as served.</param>
/// <param name="Drift">Drift, as served.</param>
/// <param name="Inputs">Inputs, as served.</param>
/// <param name="Timeout">Timeout, as served.</param>
/// <param name="Budget">Budget, as served.</param>
/// <param name="Paused">Paused, as served.</param>
/// <param name="Injection">The injection sections, as served.</param>
/// <param name="Commands">Commands, as served.</param>
/// <param name="Text">Text, as served.</param>
internal sealed record StatusFields(
    StatusHeader Header,
    string? Next,
    StatusAnswer? Answer,
    StatusChild? Child,
    StatusDrift? Drift,
    IReadOnlyList<StatusInput> Inputs,
    StatusTimeout? Timeout,
    StatusBudget? Budget,
    StatusPaused? Paused,
    IReadOnlyList<StatusCommand> Commands,
    string Text,
    IReadOnlyList<StatusInjection>? Injection = null)
{
    /// <summary>The injection sections, never null.</summary>
    public IReadOnlyList<StatusInjection> Injection { get; init; } = Injection ?? [];
}
