using Aw.Sindri.Core.Sessions;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every <see cref="ITeamNames"/> keeps: each team has a name, no name twice, and no teams (or none that read) answers an empty list rather than
/// throwing. The fake and the team-file adapter run it.
/// </summary>
public abstract class TeamNamesContract
{
    /// <summary>A source with at least one team.</summary>
    /// <returns>The subject.</returns>
    protected abstract ITeamNames Create();

    /// <summary>A source with no team.</summary>
    /// <returns>The subject.</returns>
    protected abstract ITeamNames CreateEmpty();

    [Fact]
    public void Read_lists_each_team_once_by_a_non_empty_name()
    {
        var names = Create().Read().Select(static t => t.Team).ToList();

        Expect.True(names.Count > 0);
        Expect.True(names.TrueForAll(static n => n.Length > 0));
        Expect.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Read_without_teams_is_empty_and_never_throws() =>
        Expect.Equal(0, CreateEmpty().Read().Count);
}
