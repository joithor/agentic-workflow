namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Whether a declared input is present.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Required">Required, as served.</param>
/// <param name="Present">Present, as served.</param>
internal sealed record InputPresence(
    string Key,
    string Label,
    string Kind,
    bool Required,
    bool Present);
