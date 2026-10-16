namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The badge for a state the engine owns.</summary>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Reason">Reason, as served.</param>
internal sealed record EngineOwnedBadge(
    EngineOwnedKind Kind,
    string Reason);
