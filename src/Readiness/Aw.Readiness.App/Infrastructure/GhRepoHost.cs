using System.Globalization;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>The gh CLI adapter of <see cref="IRepoHost"/>: read-only 'gh repo view' and 'gh api' GETs, run in the
/// checkout so gh picks the repo from its remote. Anything gh can't answer is unknown, with gh's reason.</summary>
/// <param name="settings">Which gh to run and its timeout.</param>
internal sealed class GhRepoHost(ReadinessSettings settings) : IRepoHost
{
    private readonly GhCli _gh = new(settings);

    public async ValueTask<HostFacts> Read(string root, CancellationToken cancellationToken)
    {
        var view = await _gh.Run(root, ["repo", "view", "--json", "nameWithOwner,defaultBranchRef"], cancellationToken).ConfigureAwait(false);
        if (!view.Ok)
        {
            return HostFacts.Unknown(view.Ran ? $"gh repo view failed: {view.Reason}" : view.Reason);
        }

        if (!GhJson.TryRepo(view.Stdout, out var name, out var branch))
        {
            return HostFacts.Unknown("gh repo view printed no repo name and default branch");
        }

        var api = $"repos/{name}";
        return new HostFacts(
            await Protection(root, api, branch, cancellationToken).ConfigureAwait(false),
            await Scanning(root, api, cancellationToken).ConfigureAwait(false),
            await Releases(root, api, cancellationToken).ConfigureAwait(false),
            await Labels(root, api, cancellationToken).ConfigureAwait(false));
    }

    private async Task<HostFact> Protection(string root, string api, string branch, CancellationToken ct)
    {
        var protection = await _gh.Run(root, ["api", $"{api}/branches/{branch}/protection"], ct).ConfigureAwait(false);
        if (protection.Ok)
        {
            return new HostFact(Holds: true, $"branch protection is on for {branch}");
        }

        if (protection.IsHttp(403) && protection.Reason.Contains("Upgrade to GitHub Pro", StringComparison.Ordinal))
        {
            return new HostFact(Holds: false, $"{branch} can't be protected: branch protection and rulesets aren't available for this repo on its plan (HTTP 403)");
        }

        if (!protection.IsHttp(404))
        {
            return new HostFact(Holds: null, $"gh couldn't read {branch}'s protection: {protection.Reason}");
        }

        var rules = await _gh.Run(root, ["api", $"{api}/rules/branches/{branch}"], ct).ConfigureAwait(false);
        return !rules.Ok || !GhJson.TryRuleTypes(rules.Stdout, out var types)
            ? new HostFact(Holds: null, $"gh couldn't read the ruleset rules for {branch}: {rules.Reason}")
            : types.Count > 0
                ? new HostFact(Holds: true, $"ruleset rules apply to {branch}: {string.Join(", ", types)}")
                : new HostFact(Holds: false, $"{branch} is not protected: no branch protection (HTTP 404) and no ruleset rules");
    }

    private async Task<HostFact> Scanning(string root, string api, CancellationToken ct)
    {
        var repo = await _gh.Run(root, ["api", api], ct).ConfigureAwait(false);
        if (!repo.Ok)
        {
            return new HostFact(Holds: null, $"gh couldn't read the repo: {repo.Reason}");
        }

        return GhJson.Scanning(repo.Stdout) switch
        {
            (not true, _) => new HostFact(Holds: null, "secret scanning isn't reported to this login (not a repo admin)"),
            (_, null) => new HostFact(Holds: false, "secret scanning isn't available for this repo (security_and_analysis is empty)"),
            (_, var (scanning, push)) => new HostFact(
                Holds: scanning == "enabled" && push == "enabled",
                $"secret scanning {scanning}, push protection {push}"),
        };
    }

    private async Task<HostLabels> Labels(string root, string api, CancellationToken ct)
    {
        var labels = await _gh.Run(root, ["api", $"{api}/labels?per_page=100"], ct).ConfigureAwait(false);
        return labels.Ok && GhJson.TryNames(labels.Stdout, out var names)
            ? new HostLabels(names, string.Create(CultureInfo.InvariantCulture, $"{names.Count} labels on the repo (the first 100 read)"))
            : new HostLabels(Names: null, $"gh couldn't read the labels: {labels.Reason}");
    }

    private async Task<HostReleases> Releases(string root, string api, CancellationToken ct)
    {
        var releases = await _gh.Run(root, ["api", $"{api}/releases?per_page=100"], ct).ConfigureAwait(false);
        return releases.Ok && GhJson.TryPublished(releases.Stdout, out var published)
            ? new HostReleases(published, string.Create(CultureInfo.InvariantCulture, $"{published.Count} published releases (the newest 100 read)"))
            : new HostReleases(PublishedAt: null, $"gh couldn't read the releases: {releases.Reason}");
    }
}
