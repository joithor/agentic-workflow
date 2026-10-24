using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>
/// Briefs from the onyx source (S-5A) through its own allowlisted routes only: a search for each profile team's key, the issues whose Onyx alias is
/// 'TEAM-n', then each record's text. Onyx's admin search takes no offset, so a team's listing cannot be paged: a search that returns the 50 hits Onyx
/// allows is reported as capped, and the window's start goes into the search as an update-time filter so the cap spends its hits inside the window. It is the eval harness's read, not the onyx arm. Onyx keeps no content history, so a record edited after its
/// creation is leaky (spec §9.1, §12); the corpus labels it. The native Linear brief, with the project's PRD and TDD documents, comes with S-6.
/// </summary>
internal sealed class OnyxBriefSource(ISourceCatalog catalog) : IBriefSource
{
    private const int MaxHits = 250;   // the most one team search asks Onyx for; a search that returns all of them is reported as capped
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(30);
    private const string OnyxId = "onyx";

    public async ValueTask<Result<BriefListing>> Issues(BriefScope scope, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var source = Onyx(scope);
        if (source is null || scope.Settings.LinearTeams.Count == 0)
        {
            return NoSource();
        }

        var issues = new List<BriefIssue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var capped = new List<string>();
        foreach (var team in scope.Settings.LinearTeams)
        {
            var listed = await Team(source, (scope, team, since), cancellationToken).ConfigureAwait(false);
            if (listed.IsError)
            {
                return listed.Error;
            }

            if (listed.Value.Capped)
            {
                capped.Add(team);
            }

            issues.AddRange(listed.Value.Issues.Where(i => seen.Add(i.Key)));
        }

        return new BriefListing([.. issues.OrderBy(i => i.Key, StringComparer.Ordinal)], capped);
    }

    private static async ValueTask<Result<(List<BriefIssue> Issues, bool Capped)>> Team(IContextSource source, (BriefScope Scope, string Team, DateTimeOffset? Since) what, CancellationToken cancellationToken)
    {
        var (scope, team, since) = what;
        var hits = await source.Search(new SourceQuery([team], AsOf: null, MaxHits, _deadline) { UpdatedSince = since }, cancellationToken).ConfigureAwait(false);
        if (hits.IsError)
        {
            return ContextErrors.AllSourcesUnavailable.With($"the brief source can't be searched ({hits.Error.Code}); fix: sindri context sources status");
        }

        var issues = new List<BriefIssue>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hit in hits.Value)
        {
            var key = hit.Aliases.FirstOrDefault(a => IssueKey(team).IsMatch(a));
            if (key is null || !keys.Add(key))
            {
                continue;
            }

            var issue = await Issue(source, scope, (key, hit), cancellationToken).ConfigureAwait(false);
            if (issue.IsError)
            {
                return issue.Error;   // an Onyx that fails mid-listing fails the run: a corpus that shrank in silence would pass for a small sample
            }

            if (issue.Value is { } found)
            {
                issues.Add(found);
            }
        }

        return (issues, Capped: hits.Value.Count >= MaxHits);   // every hit Onyx would give is used up and the route has no next page
    }

    public async ValueTask<Result<Brief?>> Brief(BriefScope scope, BriefIssue issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(issue);
        var source = Onyx(scope);
        if (source is null)
        {
            return NoSource();
        }

        var id = issue.RecordRef.StartsWith("onyx/", StringComparison.Ordinal) ? issue.RecordRef["onyx/".Length..] : issue.RecordRef;
        if (!ExternalId.TryFrom(id, out var external) || !RecordKey.TryFrom("linear:issue/" + id, out var canonical) || !SourceVersion.TryFrom(issue.Version ?? "unversioned", out var version))
        {
            return Results.Success<Brief?>(value: null);
        }

        var record = await source.Fetch(new SourceRef(SourceId.From(OnyxId), external, version, Permalink: null, canonical), cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error.Kind == ErrorKind.NotFound
                ? Results.Success<Brief?>(value: null)   // the record is gone: the sample is dropped
                : ContextErrors.AllSourcesUnavailable.With($"the brief source can't be read ({record.Error.Code}); fix: sindri context sources status");
        }

        var clean = scope.Scrubbers.Ingest.Clean(issue.Title + "\n\n" + record.Value.Text, singleLine: false);
        return Results.Success<Brief?>(new Brief(issue.Key, clean.Blocked ? string.Empty : clean.Text));
    }

    private static async ValueTask<Result<BriefIssue?>> Issue(IContextSource source, BriefScope scope, (string Key, SourceHit Hit) found, CancellationToken cancellationToken)
    {
        var record = await source.Fetch(found.Hit.Ref, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error.Kind == ErrorKind.NotFound
                ? Results.Success<BriefIssue?>(value: null)   // the record is gone: not a sample
                : ContextErrors.AllSourcesUnavailable.With($"the brief source can't be read ({record.Error.Code}); fix: sindri context sources status");
        }

        if (record.Value.CreatedAt is not { } created)
        {
            return Results.Success<BriefIssue?>(value: null);   // no creation time, no cut: not a sample
        }

        var updated = record.Value.UpdatedAt ?? created;
        var title = scope.Scrubbers.Ingest.Clean(found.Hit.Title, singleLine: true);
        return Results.Success<BriefIssue?>(new BriefIssue(found.Key, "onyx/" + found.Hit.Ref.Id.Value, found.Hit.Ref.Version.Value, created, updated, title.Blocked ? string.Empty : title.Text));
    }

    private IContextSource? Onyx(BriefScope scope) =>
        scope.Settings.Onyx is null ? null : catalog.For(scope.Settings, scope.Repos, scope.Scrubbers).FirstOrDefault(s => string.Equals(s.Describe().Id.Value, OnyxId, StringComparison.Ordinal));

    private static Error NoSource() =>
        ContextErrors.NoBriefSource.With("no configured source can supply issue briefs: needs 'context.sources.onyx' and 'context.sources.linear.teams' in the approved profile (fix: sindri context sources status)");

    private static Regex IssueKey(string team) =>
        new("^" + Regex.Escape(team) + "-[0-9]{1,7}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
}
