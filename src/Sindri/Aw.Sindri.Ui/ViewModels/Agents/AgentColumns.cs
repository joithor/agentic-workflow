namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The captions over the Agents table.</summary>
/// <param name="Status">The status column.</param>
/// <param name="Agent">The provider and title column.</param>
/// <param name="Repo">The Repository column's head.</param>
/// <param name="Where">The host and lane column.</param>
/// <param name="Doing">The asking or step column.</param>
/// <param name="For">How long it has been in its status.</param>
internal sealed record AgentColumns(string Status, string Agent, string Repo, string Where, string Doing, string For);
