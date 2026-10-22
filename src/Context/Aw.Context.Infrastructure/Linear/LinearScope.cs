using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// The profile's teams as the adapter applies them (spec §5.2: "by the profile's teams"): an issue is in scope by its team key; a project by any of its teams; a
/// document or project update by its project (Linear's filters reach them through the project). The projects are read once per source (up to 250, newest change first) and kept.
/// </summary>
/// <param name="client">The client.</param>
/// <param name="teams">The profile's team keys; empty scopes nothing out.</param>
internal sealed class LinearScope(LinearClient client, IReadOnlySet<string> teams)
{
    private readonly IReadOnlySet<string> _teams = teams;
    private const int MaxProjects = 250;
    private IReadOnlyList<LinearProject>? _projects;

    /// <summary>Whether the profile names teams at all.</summary>
    public bool IsScoped => _teams.Count > 0;

    /// <summary>The profile's team keys.</summary>
    public IReadOnlyCollection<string> Teams => _teams;

    /// <summary>Whether a team key is one the profile reads.</summary>
    /// <param name="key">The team key.</param>
    /// <returns>True when the profile names no teams or names this one.</returns>
    public bool Reads(string? key) => !IsScoped || (key is not null && _teams.Contains(key));

    /// <summary>The projects of the profile's teams.</summary>
    /// <param name="deadline">How long Linear gets to answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The projects, newest change first, or the error.</returns>
    public async ValueTask<Result<IReadOnlyList<LinearProject>>> Projects(TimeSpan deadline, CancellationToken cancellationToken)
    {
        if (_projects is not null)
        {
            return Kernel.Results.Success(_projects);
        }

        var data = await client.Query<LinearProjectsData>("Projects", LinearFilters.Variables(filter: null, MaxProjects), deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        _projects = [.. data.Value.Projects.Nodes.Where(InScope).OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id, StringComparer.Ordinal)];
        return Kernel.Results.Success(_projects);
    }

    /// <summary>Whether a project is one of the profile's.</summary>
    /// <param name="project">The project with its teams.</param>
    /// <returns>True when the profile names no teams or the project has one of them.</returns>
    public bool InScope(LinearProject project) => !IsScoped || (project.Teams?.Nodes ?? []).Any(t => _teams.Contains(t.Key));
}
