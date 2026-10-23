namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>GET /sindri/code-index/status: one row per repo in the caller's scope (code-index spec §7.3).</summary>
/// <param name="Repos">The repos' indexes.</param>
/// <param name="Health">Each repo's health overlay; empty from a server that predates it.</param>
internal sealed record CodeIndexStatusView(IReadOnlyList<CodeIndexRepoView> Repos, IReadOnlyList<CodeIndexHealthView>? Health = null);
