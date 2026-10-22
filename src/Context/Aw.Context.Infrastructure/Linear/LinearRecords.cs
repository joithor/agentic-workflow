using System.Diagnostics;
using System.Text.Json.Nodes;
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
    private const int LatestUpdates = 5;
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

        var allowed = await InProfile(document.Project?.Id, Remaining(deadline), cancellationToken).ConfigureAwait(false);
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

        if (data.Value.Projects.Nodes is not [var project, ..] || !scope.InScope(project))
        {
            return NotFound($"project {id} is gone, or isn't in the profile's teams");
        }

        if (Changed(reference, project.UpdatedAt) is { } changed)
        {
            return changed;
        }

        var updates = await client.Pages(new LinearList<LinearUpdatesData, LinearProjectUpdate>("ProjectUpdates", LinearFilters.InProject(id), LatestUpdates, static d => d.ProjectUpdates), Remaining(deadline), cancellationToken).ConfigureAwait(false);
        return updates.IsError
            ? updates.Error
            : new SourceRecord(reference, RecordKind.Project, project.Name, LinearText.Of(project, [.. updates.Value.OrderByDescending(static u => u.UpdatedAt).Take(LatestUpdates)]), project.Lead?.Name, project.CreatedAt, project.UpdatedAt);
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

        var allowed = await InProfile(update.Project?.Id, Remaining(deadline), cancellationToken).ConfigureAwait(false);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        return !allowed.Value
            ? NotFound($"project update {id} isn't in a project of the profile's teams")
            : AtCitedVersion(reference, update.UpdatedAt, () =>
                new SourceRecord(reference, RecordKind.Project, $"Project update: {update.Project?.Name ?? "project"} ({LinearText.Day(update.CreatedAt)})", LinearText.Of(update), update.User?.Name, update.CreatedAt, update.UpdatedAt));
    }

    // One small project read with the profile's teams in its filter: the answer does not depend on how many projects the workspace has.
    private async ValueTask<Result<bool>> InProfile(string? projectId, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        if (!scope.IsScoped)
        {
            return true;
        }

        var projects = await scope.ProjectsOf([projectId], left, cancellationToken).ConfigureAwait(false);
        return projects.IsError ? projects.Error : projectId is not null && projects.Value.Contains(projectId);
    }

    private static Func<TimeSpan> Remaining(TimeSpan deadline)
    {
        var started = Stopwatch.GetTimestamp();
        return () => deadline - Stopwatch.GetElapsedTime(started);
    }

    // The versions of records the profile reads, as Fetch reads them: an issue by its team, a project by its teams, a document or an update by its project.
    private ValueTask<Result<IReadOnlyList<(string Id, string Version)>>> VersionsOf(string prefix, string[] ids, TimeSpan deadline, CancellationToken cancellationToken)
    {
        var (left, filter) = (Remaining(deadline), LinearFilters.WithIds(ids));
        return prefix switch
        {
            IssuePrefix => IssueVersions(filter, ids.Length, left, cancellationToken),
            DocPrefix => DocumentVersions(filter, ids.Length, left, cancellationToken),
            ProjectPrefix => ProjectVersions(filter, ids.Length, left, cancellationToken),
            _ => UpdateVersions(filter, ids.Length, left, cancellationToken),
        };
    }

    private async ValueTask<Result<IReadOnlyList<(string Id, string Version)>>> IssueVersions(JsonObject filter, int count, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var issues = await client.Pages(new LinearList<LinearIssuesData, LinearIssue>("IssueHits", filter, count, static d => d.Issues), left, cancellationToken).ConfigureAwait(false);
        return issues.IsError ? issues.Error : Found(issues.Value.Where(i => scope.Reads(i.Team?.Key)).Select(i => (i.Id, i.UpdatedAt)));
    }

    private async ValueTask<Result<IReadOnlyList<(string Id, string Version)>>> DocumentVersions(JsonObject filter, int count, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var documents = await client.Pages(new LinearList<LinearDocumentsData, LinearDocument>("DocumentHits", filter, count, static d => d.Documents), left, cancellationToken).ConfigureAwait(false);
        return documents.IsError ? documents.Error : await InProfileOnly(documents.Value.Select(d => (d.Id, d.UpdatedAt, d.Project?.Id)), left, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<IReadOnlyList<(string Id, string Version)>>> ProjectVersions(JsonObject filter, int count, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var projects = await client.Pages(new LinearList<LinearProjectsData, LinearProject>("Projects", filter, count, static d => d.Projects), left, cancellationToken).ConfigureAwait(false);
        return projects.IsError ? projects.Error : Found(projects.Value.Where(scope.InScope).Select(p => (p.Id, p.UpdatedAt)));
    }

    private async ValueTask<Result<IReadOnlyList<(string Id, string Version)>>> UpdateVersions(JsonObject filter, int count, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var updates = await client.Pages(new LinearList<LinearUpdatesData, LinearProjectUpdate>("ProjectUpdates", filter, count, static d => d.ProjectUpdates), left, cancellationToken).ConfigureAwait(false);
        return updates.IsError ? updates.Error : await InProfileOnly(updates.Value.Select(u => (u.Id, u.UpdatedAt, u.Project?.Id)), left, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<IReadOnlyList<(string Id, string Version)>>> InProfileOnly(IEnumerable<(string Id, DateTimeOffset UpdatedAt, string? Project)> records, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var all = records.ToList();
        var allowed = await scope.ProjectsOf(all.Select(static r => r.Project), left, cancellationToken).ConfigureAwait(false);
        return allowed.IsError ? allowed.Error : Found(all.Where(r => !scope.IsScoped || (r.Project is not null && allowed.Value.Contains(r.Project))).Select(static r => (r.Id, r.UpdatedAt)));
    }

    private static Result<IReadOnlyList<(string Id, string Version)>> Found(IEnumerable<(string Id, DateTimeOffset UpdatedAt)> nodes) =>
        Results.Success<IReadOnlyList<(string Id, string Version)>>([.. nodes.Select(n => (n.Id, LinearHits.Version(n.UpdatedAt)))]);

    private static string Prefix(string key) =>
        key.StartsWith(IssuePrefix, StringComparison.Ordinal) ? IssuePrefix
        : key.StartsWith(DocPrefix, StringComparison.Ordinal) ? DocPrefix
        : key.StartsWith(ProjectPrefix, StringComparison.Ordinal) ? ProjectPrefix
        : key.StartsWith(UpdatePrefix, StringComparison.Ordinal) ? UpdatePrefix
        : "";

    /// <summary>The record while it is at the version cited, else NotFound.</summary>
    private static Result<SourceRecord> AtCitedVersion(SourceRef reference, DateTimeOffset updatedAt, Func<SourceRecord> record) =>
        Changed(reference, updatedAt) is { } changed ? changed : record();

    // The error for a record that moved on since it was cited, or null while it is still at the cited version.
    private static Error? Changed(SourceRef reference, DateTimeOffset updatedAt)
    {
        var now = LinearHits.Version(updatedAt);
        return string.Equals(now, reference.Version.Value, StringComparison.Ordinal)
            ? null
            : ContextErrors.LinearRecordNotFound.With($"Linear's {reference.Canonical.Value} changed since it was cited (cited {reference.Version.Value}, now {now}); Linear keeps no earlier version.");
    }

    private static Error NotFound(string what) => ContextErrors.LinearRecordNotFound.With($"Linear has no such record: {what}.");
}
