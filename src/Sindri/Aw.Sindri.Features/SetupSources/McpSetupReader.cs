using Aw.Kernel;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the per-repo MCP registration: for each repo the approved profile names, whether Claude Code's local-scope entry exists for its checkout.
/// Only local scope counts; a user-wide entry is never looked for here (doctor warns on that) and a file that can't be read is no entry. It starts nothing.
/// </summary>
/// <param name="approvals">The approved profile (the repos and their checkouts).</param>
/// <param name="host">Reads Claude Code's config file.</param>
internal sealed class McpSetupReader(IApprovedProfile approvals, IProviderHost host)
{
    internal async ValueTask<Result<McpFact>> Read(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var repos = approved.Value?.Profile.Repos ?? [];
        if (repos.Count == 0)
        {
            return new McpFact(0, 0);
        }

        var text = await Config(cancellationToken).ConfigureAwait(false);
        return new McpFact(repos.Count, repos.Count(r => McpLocalRegistration.IsRegistered(text, r.Path, r.Name)));
    }

    private async ValueTask<string?> Config(CancellationToken cancellationToken)
    {
        try
        {
            return await host.Read(host.UserConfigPath(ProviderFile.Claude), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // as registering reads it: a file that can't be read is no entry
        }
    }
}
