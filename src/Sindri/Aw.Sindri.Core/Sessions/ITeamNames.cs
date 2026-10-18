namespace Aw.Sindri.Core.Sessions;

/// <summary>The Meta teams' names and lanes, to tell a team's tmux socket from another (collector spec §6.1).</summary>
public interface ITeamNames
{
    /// <summary>The teams under the state directory's meta folder; empty when there are none or none reads.</summary>
    /// <returns>The teams.</returns>
    public IReadOnlyList<TeamNames> Read();
}
