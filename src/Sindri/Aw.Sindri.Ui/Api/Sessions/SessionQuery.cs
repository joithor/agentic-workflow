namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>What GET /sessions is asked for; every part optional. The Agents page filters in the app over one unfiltered read, so it sends none.</summary>
/// <param name="Provider">Only this provider.</param>
/// <param name="State">Only this state.</param>
/// <param name="Repo">Only this repo.</param>
/// <param name="InWorkflow">Only sessions in (true) or outside (false) a workflow.</param>
internal sealed record SessionQuery(string? Provider = null, string? State = null, string? Repo = null, bool? InWorkflow = null);
