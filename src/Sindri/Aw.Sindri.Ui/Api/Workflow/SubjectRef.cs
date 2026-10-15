namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What an instance is about.</summary>
/// <param name="Kind">The kind the first input declares, or null when it is a link or payload with none (engine #227): then the app shows the key.</param>
/// <param name="Key">Key, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Href">Href, as served.</param>
internal sealed record SubjectRef(
    string? Kind,
    string Key,
    string Label,
    string? Href);
