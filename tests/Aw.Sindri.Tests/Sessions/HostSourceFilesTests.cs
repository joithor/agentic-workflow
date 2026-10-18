using System.Runtime.Versioning;
using System.Text;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The files host detection reads: terminals.json, /proc/net/unix and the Meta team files (collector spec CO-2b).</summary>
[UnsupportedOSPlatform("windows")]
public sealed class HostSourceFilesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("aw-hostfiles").FullName;

    [Fact, Trait("Lane", "unit")]
    public void TerminalProcessNames_reads_every_row_of_terminals_json()
    {
        var names = TerminalProcessNames.Parse(Encoding.UTF8.GetBytes("""{ "schema": 1, "terminals": [ { "id": "kitty", "name": "kitty", "bin": "kitty", "termProgram": [], "argv": ["kitty"], "processNames": ["kitty"], "other": 1 }, { "id": "plain", "name": "plain", "bin": "plain", "termProgram": [], "argv": ["plain"] } ] }""")).All;

        Assert.Equal("kitty,plain", string.Join(',', names.Select(static n => n.Name)));
        Assert.Equal("kitty", string.Join(',', names[0].ProcessNames));
        Assert.Empty(names[1].ProcessNames);
        Assert.Empty(TerminalProcessNames.Parse("not json"u8.ToArray()).All);
    }

    [Fact, Trait("Lane", "unit")]
    public void TerminalProcessNames_embedded_holds_the_shipped_terminals()
    {
        var names = TerminalProcessNames.Embedded().All;

        Assert.Contains(names, static n => n.Name == "kitty" && n.ProcessNames.Contains("kitty", StringComparer.Ordinal));
        Assert.All(names, static n => Assert.NotEmpty(n.ProcessNames));
    }

    [Fact, Trait("Lane", "integration")]
    public void UnixSockets_maps_inode_to_path_and_skips_unbound_sockets()
    {
        var sockets = new UnixSockets(SessionFixtures.Proc).Read();

        Assert.Equal("/tmp/tmux-1000/t1", sockets[880001]);
        Assert.False(sockets.ContainsKey(880002));   // no path: not bound
        Assert.Equal("/run/user/1000/bus", sockets[770001]);
        Assert.Empty(new UnixSockets(Path.Combine(_directory, "nope")).Read());
    }

    [Fact, Trait("Lane", "integration")]
    public void TeamNameFiles_reads_team_and_lanes_only_from_the_default_and_named_teams()
    {
        Write("meta/team.json", """{ "team": "t1", "lanes": ["api", "web"], "extra": "x" }""");
        Write("meta/teams/b/team.json", """{ "team": "b", "lanes": [] }""");
        Write("meta/teams/dup/team.json", """{ "team": "t1", "lanes": ["z"] }""");
        Write("meta/teams/bad/team.json", "{ not json");

        var teams = new TeamNameFiles(_directory).Read();

        Assert.Equal("t1,b", string.Join(',', teams.Select(static t => t.Team)));
        Assert.Equal("api,web", string.Join(',', teams[0].Lanes));
        Assert.Empty(new TeamNameFiles(Path.Combine(_directory, "nowhere")).Read());
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_directory, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
