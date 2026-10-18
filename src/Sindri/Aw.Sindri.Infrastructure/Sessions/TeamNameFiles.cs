using Aw.Kernel.Json;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The Meta teams' names and lanes (collector spec §6.1): &lt;state&gt;/meta/team.json and &lt;state&gt;/meta/teams/*/team.json, read for 'team' and 'lanes' only.</summary>
/// <param name="stateDirectory">AW_STATE_DIR.</param>
internal sealed class TeamNameFiles(string stateDirectory) : ITeamNames
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SessionsJsonContext.Default);

    public IReadOnlyList<TeamNames> Read()
    {
        var meta = Path.Combine(stateDirectory, "meta");
        var files = new List<string> { Path.Combine(meta, "team.json") };
        try
        {
            files.AddRange(Directory.EnumerateDirectories(Path.Combine(meta, "teams")).Order(StringComparer.Ordinal).Select(static d => Path.Combine(d, "team.json")));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // no named teams
        }

        return [.. files.Select(One).OfType<TeamNames>().DistinctBy(static t => t.Team, StringComparer.Ordinal)];
    }

    private static TeamNames? One(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var file = AwJson.Deserialize<TeamFile>(bytes, _json);
            return file.IsSuccess && file.Value is { Team: { Length: > 0 } team, Lanes: { } lanes } ? new TeamNames(team, [.. lanes]) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
