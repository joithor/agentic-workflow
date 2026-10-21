using Aw.Kernel;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the per-repo MCP registration: for each repo the approved profile names, whether each installed provider's local registration exists for its checkout (Claude Code's local-scope entry, Codex's and Cursor's project files).
/// Only local scope counts; a user-wide entry is never looked for here (doctor warns on that) and a file that can't be read is no entry. It starts nothing.
/// </summary>
/// <param name="approvals">The approved profile (the repos and their checkouts).</param>
/// <param name="host">Reads the providers' config files and says which are installed.</param>
internal sealed class McpSetupReader(IApprovedProfile approvals, IProviderHost host)
{
    private static readonly (ProviderFile Provider, string Name)[] _providers = [(ProviderFile.Claude, "Claude Code"), (ProviderFile.Codex, "Codex"), (ProviderFile.Cursor, "Cursor")];

    internal async ValueTask<Result<McpFact>> Read(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var installed = _providers.Where(p => host.Installed(p.Provider)).ToList();
        var names = string.Join(", ", installed.Select(static p => p.Name));
        var repos = approved.Value?.Profile.Repos ?? [];
        if (repos.Count == 0 || installed.Count == 0)
        {
            return new McpFact(repos.Count, 0, names);
        }

        var claude = installed.Exists(static p => p.Provider == ProviderFile.Claude) ? await Text(host.UserConfigPath(ProviderFile.Claude), cancellationToken).ConfigureAwait(false) : null;
        var lines = new List<SetupItem>();
        foreach (var repo in repos)
        {
            lines.Add(await Line(installed, claude, repo, cancellationToken).ConfigureAwait(false));
        }

        return new McpFact(repos.Count, lines.Count(static l => l.State == SetupState.Done), names) { Items = new SetupItems(lines) };
    }

    private async ValueTask<SetupItem> Line(List<(ProviderFile Provider, string Name)> installed, string? claude, RepoEntry repo, CancellationToken cancellationToken)
    {
        var registeredIn = new List<string>();
        foreach (var (provider, name) in installed)
        {
            if (await IsRegistered(provider, claude, repo, cancellationToken).ConfigureAwait(false))
            {
                registeredIn.Add(name);
            }
        }

        return new SetupItem(repo.Name, registeredIn.Count == installed.Count ? SetupState.Done : SetupState.Todo, registeredIn.Count == 0 ? null : string.Join(", ", registeredIn));
    }

    private async ValueTask<bool> IsRegistered(ProviderFile provider, string? claude, RepoEntry repo, CancellationToken cancellationToken) => provider switch
    {
        ProviderFile.Claude => McpLocalRegistration.IsRegistered(claude, repo.Path, repo.Name),
        ProviderFile.Cursor => McpLocalRegistration.IsRegisteredCursor(await Text(Path.Combine(repo.Path, ".cursor", "mcp.json"), cancellationToken).ConfigureAwait(false), repo.Name),
        ProviderFile.Codex => McpLocalRegistration.IsRegisteredCodex(await Text(Path.Combine(repo.Path, ".codex", "config.toml"), cancellationToken).ConfigureAwait(false), repo.Name),
        _ => false,
    };

    private async ValueTask<string?> Text(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await host.Read(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // as registering reads it: a file that can't be read is no entry
        }
    }
}
