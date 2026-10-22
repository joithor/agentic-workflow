using Aw.Context.Core;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// Fetches Linear records by reference and checks their versions (spec §5.2). Linear keeps no past versions of an issue or a document, so a fetch returns the record only while it is still at the version the
/// reference cites: another updatedAt, a record outside the profile's teams, or one that is gone is NotFound (073). Versions asks once per kind of record, in batches of at most 100 ids.
/// </summary>
/// <param name="client">The client.</param>
/// <param name="scope">The profile's teams and projects.</param>
internal sealed class LinearRecords(LinearClient client, LinearScope scope)
{
    private const int Batch = 100;
    private const string IssuePrefix = "linear:issue/";
    private const string DocPrefix = "linear:doc/";
    private const string ProjectPrefix = "linear:project/";
    private const string UpdatePrefix = "linear:update/";

    /// <summary>Fetches a record.</summary>
    /// <param name="reference">The record at the version cited.</param>
    /// <param name="deadline">How long Linear gets to answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, or NotFound 073.</returns>
    public async ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, TimeSpan deadline, CancellationToken cancellationToken)
    {
        var key = reference.Canonical.Value;
        var id = reference.Id.Value;
        return key switch
        {
            _ when key.StartsWith(IssuePrefix, StringComparison.Ordinal) => await Issue(reference, id, deadline, cancellationToken).ConfigureAwait(false),
            _ when key.StartsWith(DocPrefix, StringComparison.Ordinal) => await Document(reference, id, deadline, cancellationToken).ConfigureAwait(false),
            _ when key.StartsWith(ProjectPrefix, StringComparison.Ordinal) => await Project(reference, id, deadline, cancellationToken).ConfigureAwait(false),
            _ when key.StartsWith(UpdatePrefix, StringComparison.Ordinal) => await Update(reference, id, deadline, cancellationToken).ConfigureAwait(false),
            _ => NotFound($"{key} is not a record the linear source reads"),
        };
    }

    /// <summary>The current version of each reference.</summary>
    /// <param name="refs">The references.</param>
    /// <param name="deadline">How long Linear gets to answer each call.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One entry per reference in order: its version now, or null when the record is gone (or isn't a Linear record).</returns>
    public async ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, TimeSpan deadline, CancellationToken cancellationToken)
    {
        var current = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in refs.GroupBy(r => Prefix(r.Canonical.Value), StringComparer.Ordinal).Where(g => g.Key.Length > 0))
        {
            foreach (var batch in group.Select(r => r.Id.Value).Distinct(StringComparer.Ordinal).Chunk(Batch))
            {
                var versions = await VersionsOf(group.Key, batch, deadline, cancellationToken).ConfigureAwait(false);
                if (versions.IsError)
                {
                    return versions.Error;
                }

                foreach (var (id, version) in versions.Value)
                {
                    current[group.Key + id] = version;
                }
            }
        }

        return refs.Select(r => current.TryGetValue(Prefix(r.Canonical.Value) + r.Id.Value, out var v) ? (SourceVersion?)SourceVersion.From(v) : null).ToList();
    }

    private async ValueTask<Result<SourceRecord>> Issue(SourceRef reference, string id, TimeSpan deadline, CancellationToken cancellationToken)
    {
        var data = await client.Query<LinearIssueData>("Issue", LinearFilters.ById(id), deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        if (data.Value.Issue is not { } issue || !scope.Reads(issue.Team?.Key))
        {
            return NotFound($"issue {id} is gone, or isn't in the profile's teams");
        }

        return AtCitedVersion(reference, issue.UpdatedAt, () =>
            new SourceRecord(reference, RecordKind.Issue, LinearHits.Title(issue), LinearText.Of(issue), issue.Creator?.Name, issue.CreatedAt, issue.UpdatedAt));
    }

    private async ValueTask<Result<SourceRecord>> Document(SourceRef reference, string id, TimeSpan deadline, CancellationToken cancellationToken)
    {
        var data = await client.Query<LinearDocumentData>("Document", LinearFilters.ById(id), deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        if (data.Value.Document is not { } document)
        {
            return NotFound($"document {id} is gone");
        }

        var allowed = await InProfile(document.Project?.Id, deadline, cancellationToken).ConfigureAwait(false);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        return !allowed.Value
            ? NotFound($"document {id} isn't in a project of the profile's teams")
            : AtCitedVersion(reference, document.UpdatedAt, () =>
                new SourceRecord(reference, RecordKind.Doc, document.Title, LinearText.Of(document), document.Creator?.Name, document.CreatedAt, document.UpdatedAt));
    }

    private async ValueTask<Result<SourceRecord>> Project(SourceRef reference, string id, TimeSpan deadline, CancellationToken cancellationToken)
    {
        var data = await client.Query<LinearProjectsData>("Projects", LinearFilters.Variables(LinearFilters.WithIds([id]), 1), deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        return data.Value.Projects.Nodes is [var project, ..] && scope.InScope(project)
            ? AtCitedVersion(reference, project.UpdatedAt, () =>
                new SourceRecord(reference, RecordKind.Project, project.Name, LinearText.Of(project), project.Lead?.Name, project.CreatedAt, project.UpdatedAt))
            : NotFound($"project {id} is gone, or isn't in the profile's teams");
    }

    private async ValueTask<Result<SourceRecord>> Update(SourceRef reference, string id, TimeSpan deadline, CancellationToken cancellationToken)
    {
        var data = await client.Query<LinearUpdatesData>("ProjectUpdates", LinearFilters.Variables(LinearFilters.WithIds([id]), 1), deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        if (data.Value.ProjectUpdates.Nodes is not [var update, ..])
        {
            return NotFound($"project update {id} is gone");
        }

        var allowed = await InProfile(update.Project?.Id, deadline, cancellationToken).ConfigureAwait(false);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        return !allowed.Value
            ? NotFound($"project update {id} isn't in a project of the profile's teams")
            : AtCitedVersion(reference, update.UpdatedAt, () =>
                new SourceRecord(reference, RecordKind.Project, $"Project update: {update.Project?.Name ?? "project"} ({LinearText.Day(update.CreatedAt)})", LinearText.Of(update), update.User?.Name, update.CreatedAt, update.UpdatedAt));
    }

    private async ValueTask<Result<bool>> InProfile(string? projectId, TimeSpan deadline, CancellationToken cancellationToken)
    {
        if (!scope.IsScoped)
        {
            return true;
        }

        var projects = await scope.Projects(deadline, cancellationToken).ConfigureAwait(false);
        return projects.IsError ? projects.Error : projectId is not null && projects.Value.Any(p => string.Equals(p.Id, projectId, StringComparison.Ordinal));
    }

    private async ValueTask<Result<IReadOnlyList<(string Id, string Version)>>> VersionsOf(string prefix, string[] ids, TimeSpan deadline, CancellationToken cancellationToken)
    {
        var variables = LinearFilters.Variables(LinearFilters.WithIds(ids), ids.Length);
        switch (prefix)
        {
            case IssuePrefix:
                var issues = await client.Query<LinearIssuesData>("IssueHits", variables, deadline, cancellationToken).ConfigureAwait(false);
                return issues.IsError ? issues.Error : Found(issues.Value.Issues.Nodes.Select(i => (i.Id, i.UpdatedAt)));
            case DocPrefix:
                var documents = await client.Query<LinearDocumentsData>("DocumentHits", variables, deadline, cancellationToken).ConfigureAwait(false);
                return documents.IsError ? documents.Error : Found(documents.Value.Documents.Nodes.Select(d => (d.Id, d.UpdatedAt)));
            case ProjectPrefix:
                var projects = await client.Query<LinearProjectsData>("Projects", variables, deadline, cancellationToken).ConfigureAwait(false);
                return projects.IsError ? projects.Error : Found(projects.Value.Projects.Nodes.Select(p => (p.Id, p.UpdatedAt)));
            default:
                var updates = await client.Query<LinearUpdatesData>("ProjectUpdates", variables, deadline, cancellationToken).ConfigureAwait(false);
                return updates.IsError ? updates.Error : Found(updates.Value.ProjectUpdates.Nodes.Select(u => (u.Id, u.UpdatedAt)));
        }
    }

    private static Result<IReadOnlyList<(string Id, string Version)>> Found(IEnumerable<(string Id, DateTimeOffset UpdatedAt)> nodes) =>
        Kernel.Results.Success<IReadOnlyList<(string Id, string Version)>>([.. nodes.Select(n => (n.Id, LinearHits.Version(n.UpdatedAt)))]);

    private static string Prefix(string key) =>
        key.StartsWith(IssuePrefix, StringComparison.Ordinal) ? IssuePrefix
        : key.StartsWith(DocPrefix, StringComparison.Ordinal) ? DocPrefix
        : key.StartsWith(ProjectPrefix, StringComparison.Ordinal) ? ProjectPrefix
        : key.StartsWith(UpdatePrefix, StringComparison.Ordinal) ? UpdatePrefix
        : "";

    /// <summary>The record while it is at the version cited, else NotFound.</summary>
    private static Result<SourceRecord> AtCitedVersion(SourceRef reference, DateTimeOffset updatedAt, Func<SourceRecord> record)
    {
        var now = LinearHits.Version(updatedAt);
        return string.Equals(now, reference.Version.Value, StringComparison.Ordinal)
            ? record()
            : ContextErrors.LinearRecordNotFound.With($"Linear's {reference.Canonical.Value} changed since it was cited (cited {reference.Version.Value}, now {now}); Linear keeps no earlier version.");
    }

    private static Error NotFound(string what) => ContextErrors.LinearRecordNotFound.With($"Linear has no such record: {what}.");
}
