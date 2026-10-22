namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>GET /sindri/code-index/status: one row per repo in the caller's scope (code-index spec §7.3).</summary>
/// <param name="Repos">The repos' indexes.</param>
internal sealed record CodeIndexStatusView(IReadOnlyList<CodeIndexRepoView> Repos);
