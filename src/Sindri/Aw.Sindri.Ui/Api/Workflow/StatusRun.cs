namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The open Step run of an agent session in a status.</summary>
/// <param name="Runtime">Runtime, as served ('agent').</param>
/// <param name="Launch">Launch, as served.</param>
internal sealed record StatusRun(
    string Runtime,
    string? Launch);
