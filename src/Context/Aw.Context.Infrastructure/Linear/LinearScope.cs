using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// The profile's teams as the adapter applies them (spec §5.2: "by the profile's teams"): an issue is in scope by its team key; a project by any of its teams; a document or project update by its
/// project. Linear applies the teams in the filter of each read, so the adapter keeps no list of projects (a workspace of any size reads the same); a record fetched by id is checked with one
/// small project read.
/// </summary>
/// <param name="client">The client.</param>
/// <param name="teams">The profile's team keys; empty scopes nothing out.</param>
internal sealed class LinearScope(LinearClient client, IReadOnlySet<string> teams)
{
    private const int Batch = 100;
    private readonly IReadOnlySet<string> _teams = teams;

    /// <summary>Whether the profile names teams at all.</summary>
    public bool IsScoped => _teams.Count > 0;

    /// <summary>The profile's team keys.</summary>
    public IReadOnlyCollection<string> Teams => _teams;

    /// <summary>Whether a team key is one the profile reads.</summary>
    /// <param name="key">The team key.</param>
    /// <returns>True when the profile names no teams or names this one.</returns>
    public bool Reads(string? key) => !IsScoped || (key is not null && _teams.Contains(key));

    /// <summary>Whether a project is one of the profile's.</summary>
    /// <param name="project">The project with its teams.</param>
    /// <returns>True when the profile names no teams or the project has one of them.</returns>
    public bool InScope(LinearProject project) => !IsScoped || (project.Teams?.Nodes ?? []).Any(t => _teams.Contains(t.Key));

    /// <summary>Which of these projects are the profile's: one read per hundred ids, filtered by Linear on the profile's teams.</summary>
    /// <param name="projectIds">The project ids (null ones are skipped).</param>
    /// <param name="left">What is left of the deadline.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The ids that are the profile's, or the error.</returns>
    public async ValueTask<Result<HashSet<string>>> ProjectsOf(IEnumerable<string?> projectIds, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        var ids = projectIds.OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        if (!IsScoped)
        {
            return ids.ToHashSet(StringComparer.Ordinal);
        }

        var inProfile = new HashSet<string>(StringComparer.Ordinal);
        foreach (var batch in ids.Chunk(Batch))
        {
            var filter = LinearFilters.OfTeams(LinearFilters.WithIds(batch), _teams);
            var found = await client.Pages(new LinearList<LinearProjectsData, LinearProject>("Projects", filter, batch.Length, static d => d.Projects), left, cancellationToken).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            inProfile.UnionWith(found.Value.Select(static p => p.Id));
        }

        return inProfile;
    }
}
