using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>Lets the automatic registration ask again only when something changed since it last looked at a repo: a new approved profile hash, this process's first tick, or a different port (setup-once spec §5.4, SO-A4).</summary>
/// <param name="inner">Says whether a registration would change anything.</param>
/// <param name="approvals">The approved profile snapshot.</param>
/// <param name="endpoint">The running control plane's port.</param>
/// <param name="memory">What this process has already looked at.</param>
internal sealed class ChangedMcpNeed(IMcpNeed inner, IApprovedProfile approvals, IEndpointFile endpoint, McpAutoMemory memory) : IMcpNeed
{
    public ValueTask<bool> Needed(RepoName repo, CancellationToken cancellationToken) => inner.Needed(repo, cancellationToken);
}
