namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A command a status offers.</summary>
/// <param name="Verb">Verb, as served.</param>
/// <param name="ActionId">ActionId, as served.</param>
/// <param name="Class">Class, as served.</param>
/// <param name="Cli">Cli, as served.</param>
/// <param name="Confirm">Confirm, as served.</param>
internal sealed record StatusCommand(
    StatusVerb Verb,
    string ActionId,
    string Class,
    string Cli,
    string? Confirm);
