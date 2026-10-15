namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How the instance is answered.</summary>
/// <param name="Kind">Kind, as served.</param>
/// <param name="ActionId">ActionId, as served.</param>
/// <param name="Class">Class, as served.</param>
/// <param name="Cli">Cli, as served.</param>
/// <param name="AtTerminal">AtTerminal, as served.</param>
internal sealed record StatusAnswer(
    AnswerKind Kind,
    string ActionId,
    string Class,
    string Cli,
    bool AtTerminal);
