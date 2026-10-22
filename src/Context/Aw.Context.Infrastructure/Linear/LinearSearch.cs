using System.Diagnostics;
using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// One search of the linear source (spec §5.2): title and key lookup. An issue identifier among the terms is looked up by key; documents, issues and (by name) the profile's projects
/// are matched on their title, project updates on their body. The query's UpdatedSince goes to Linear as an 'updatedAt' filter and the profile's teams as a team or project filter, both
/// before the cap. Hits come in groups (key lookups, documents, projects, issues, updates), each newest change first, deduplicated by canonical key and cut to the limit.
/// </summary>
/// <param name="client">The client.</param>
/// <param name="scope">The profile's teams and projects.</param>
/// <param name="clock">When Linear answered.</param>
internal sealed partial class LinearSearch(LinearClient client, LinearScope scope, TimeProvider clock)
{
    private const int MaxTerms = 8;
    private const int MaxKeys = 3;
    private const int PerKind = 50;

    /// <summary>Searches.</summary>
    /// <param name="query">The terms, cut-off, limit and deadline.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The hits in rank order, or the first error.</returns>
    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Run(SourceQuery query, CancellationToken cancellationToken)
    {
        var terms = query.Terms.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTerms).ToList();
        if (terms.Count == 0 || query.Limit <= 0)
        {
            return Array.Empty<SourceHit>();
        }

        var started = Stopwatch.GetTimestamp();
        TimeSpan Left() => query.Deadline - Stopwatch.GetElapsedTime(started);
        var hits = new LinearHits(scope, clock.GetUtcNow(), query.AsOf);
        var all = new List<SourceHit>();
        foreach (var step in new Func<ValueTask<Result<List<SourceHit>>>>[]
        {
            () => Keys(terms, hits, Left(), cancellationToken),
            () => Documents(terms, query.UpdatedSince, hits, Left(), cancellationToken),
            () => Projects(terms, query.UpdatedSince, hits, Left(), cancellationToken),
            () => Issues(terms, query.UpdatedSince, hits, Left(), cancellationToken),
            () => Updates(terms, query.UpdatedSince, hits, Left(), cancellationToken),
        })
        {
            var group = await step().ConfigureAwait(false);
            if (group.IsError)
            {
                return group.Error;
            }

            all.AddRange(group.Value);
        }

        return all.DistinctBy(h => h.Ref.Canonical.Value, StringComparer.Ordinal).Take(query.Limit).Select((h, i) => h with { Rank = i + 1 }).ToList();
    }

    private async ValueTask<Result<List<SourceHit>>> Keys(List<string> terms, LinearHits hits, TimeSpan left, CancellationToken cancellationToken)
    {
        var found = new List<SourceHit>();
        foreach (var key in terms.Where(k => IsIdentifier(k) && scope.Reads(k[..k.IndexOf('-', StringComparison.Ordinal)])).Take(MaxKeys))
        {
            var issue = await client.Query<LinearIssueData>("Issue", LinearFilters.ById(key), left, cancellationToken).ConfigureAwait(false);
            if (issue.IsError && issue.Error.Code != ContextErrors.LinearRecordNotFound.Code)
            {
                return issue.Error;
            }

            if (issue.IsSuccess && issue.Value.Issue is { } node && scope.Reads(node.Team?.Key) && hits.Of(node) is { } hit)
            {
                found.Add(hit);
            }
        }

        return found;
    }

    private async ValueTask<Result<List<SourceHit>>> Documents(List<string> terms, DateTimeOffset? since, LinearHits hits, TimeSpan left, CancellationToken cancellationToken)
    {
        var filter = LinearFilters.Matching("title", terms, since);
        if (scope.IsScoped)
        {
            var projects = await scope.Projects(left, cancellationToken).ConfigureAwait(false);
            if (projects.IsError || projects.Value.Count == 0)
            {
                return projects.IsError ? projects.Error : new List<SourceHit>();
            }

            _ = LinearFilters.InProjects(filter, projects.Value.Select(p => p.Id));
        }

        var data = await client.Query<LinearDocumentsData>("DocumentHits", LinearFilters.Variables(filter, PerKind), left, cancellationToken).ConfigureAwait(false);
        return data.IsError ? data.Error : Newest(data.Value.Documents.Nodes, d => d.UpdatedAt, hits.Of);
    }

    private async ValueTask<Result<List<SourceHit>>> Projects(List<string> terms, DateTimeOffset? since, LinearHits hits, TimeSpan left, CancellationToken cancellationToken)
    {
        var projects = await scope.Projects(left, cancellationToken).ConfigureAwait(false);
        return projects.IsError
            ? projects.Error
            : Newest(projects.Value.Where(p => terms.Exists(t => p.Name.Contains(t, StringComparison.OrdinalIgnoreCase)) && (since is not { } at || p.UpdatedAt >= at)), p => p.UpdatedAt, hits.Of);
    }

    private async ValueTask<Result<List<SourceHit>>> Issues(List<string> terms, DateTimeOffset? since, LinearHits hits, TimeSpan left, CancellationToken cancellationToken)
    {
        var filter = LinearFilters.Matching("title", terms, since);
        if (scope.IsScoped)
        {
            _ = LinearFilters.InTeams(filter, scope.Teams);
        }

        var data = await client.Query<LinearIssuesData>("IssueHits", LinearFilters.Variables(filter, PerKind), left, cancellationToken).ConfigureAwait(false);
        return data.IsError ? data.Error : Newest(data.Value.Issues.Nodes, i => i.UpdatedAt, hits.Of);
    }

    private async ValueTask<Result<List<SourceHit>>> Updates(List<string> terms, DateTimeOffset? since, LinearHits hits, TimeSpan left, CancellationToken cancellationToken)
    {
        var filter = LinearFilters.Matching("body", terms, since);
        if (scope.IsScoped)
        {
            var projects = await scope.Projects(left, cancellationToken).ConfigureAwait(false);
            if (projects.IsError || projects.Value.Count == 0)
            {
                return projects.IsError ? projects.Error : new List<SourceHit>();
            }

            _ = LinearFilters.InProjects(filter, projects.Value.Select(p => p.Id));
        }

        var data = await client.Query<LinearUpdatesData>("ProjectUpdates", LinearFilters.Variables(filter, PerKind), left, cancellationToken).ConfigureAwait(false);
        return data.IsError ? data.Error : Newest(data.Value.ProjectUpdates.Nodes, u => u.UpdatedAt, hits.Of);
    }

    private static List<SourceHit> Newest<T>(IEnumerable<T> nodes, Func<T, DateTimeOffset> updatedAt, Func<T, SourceHit?> hit) =>
        [.. nodes.OrderByDescending(updatedAt).Select(hit).OfType<SourceHit>()];

    private static bool IsIdentifier(string term) => Identifier.IsMatch(term);

    [GeneratedRegex(@"^[A-Z][A-Z0-9]+-[0-9]+\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Identifier { get; }
}
