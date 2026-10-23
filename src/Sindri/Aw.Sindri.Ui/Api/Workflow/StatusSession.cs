namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The agent session that runs a step (<c>current.status.run.session</c>).</summary>
/// <param name="Provider">The provider (claude, codex, cursor).</param>
/// <param name="Id">The session's id.</param>
internal sealed record StatusSession(string Provider, string Id)
{
    /// <summary>The session's ref, the form its link and sheet use: provider/id.</summary>
    internal string Ref => Provider + "/" + Id;
}
