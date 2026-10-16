namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>An input line of a status.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Name">Name, as served.</param>
/// <param name="Hash">Hash, as served.</param>
internal sealed record StatusInput(
    string Key,
    string Name,
    string? Hash);
