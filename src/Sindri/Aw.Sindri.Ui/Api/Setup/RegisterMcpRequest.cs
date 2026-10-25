namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>POST /sindri/setup/mcp's body (setup-once spec §5.4 RegisterMcp, desk).</summary>
/// <param name="Repo">The repo to register Sindri in, or null for every approved repo.</param>
internal sealed record RegisterMcpRequest(string? Repo = null);
