namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/commands: the command catalog, the only list of actions the app knows (UI spec §9.1).</summary>
/// <param name="Rows">Every action id with its class, CLI form and UI status.</param>
internal sealed record CommandList(IReadOnlyList<CommandRowView> Rows);
