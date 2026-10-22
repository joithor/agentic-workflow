using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Host.Forge;
using Aw.Sindri.Testing.Forge;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Forge;

/// <summary>The real adapter over the forge package's file-backed forge, in a temporary state directory.</summary>
[Lane(TestLane.Integration)]
public sealed class ForgePullRequestsContractTests : PullRequestsContract, IDisposable
{
    private readonly List<DirectoryInfo> _directories = [];

    protected override PullRequestsUnderTest Create()
    {
        var directory = Directory.CreateTempSubdirectory("aw-sindri-pulls-");
        _directories.Add(directory);
        var provider = new ServiceCollection()
            .AddForgeFileInfrastructure(name => name == "AW_STATE_DIR" ? directory.FullName : null, new ForgeHosts(["forge.test"]))
            .BuildServiceProvider();
        var forge = provider.GetRequiredService<IForge>();
        var seed = provider.GetRequiredService<IForgeSeed>();
        string SeedThread(string link, string body) => seed.SeedThread(new SeedThreadRequest(forge.Parse(link).Value, CommentKind.Thread, "reviewer", "MEMBER", IsBot: false, "src/a.cs", body)).Value;
        return new PullRequestsUnderTest(new ForgePullRequests(forge), new PullRepo("forge.test", "acme", "widgets"), "elsewhere.test", SeedThread);
    }

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            directory.Delete(recursive: true);
        }
    }
}
