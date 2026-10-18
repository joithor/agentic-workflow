using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The Meta team-file adapter passes the team names contract on a temp state directory.</summary>
[UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class TeamNameFilesContractTests : TeamNamesContract, IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("aw-teamnames").FullName;

    protected override ITeamNames Create()
    {
        _ = Directory.CreateDirectory(Path.Combine(_directory, "meta", "teams", "b"));
        File.WriteAllText(Path.Combine(_directory, "meta", "team.json"), """{ "team": "t1", "lanes": ["api"] }""");
        File.WriteAllText(Path.Combine(_directory, "meta", "teams", "b", "team.json"), """{ "team": "b", "lanes": [] }""");
        return new TeamNameFiles(_directory);
    }

    protected override ITeamNames CreateEmpty() => new TeamNameFiles(Path.Combine(_directory, "nowhere"));

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
