namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The agent session behind a run: the app opens session/{provider}/{id}.</summary>
/// <param name="Provider">The agent ('claude-code').</param>
/// <param name="Id">The provider's session id.</param>
internal sealed record SessionHandle(string Provider, string Id);
