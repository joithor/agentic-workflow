using Aw.Evidence.Infrastructure.Files;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Evidence.Infrastructure.Targets;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real adapter over a temp repo.</summary>
[Lane(TestLane.Integration)]
public sealed class FileEvidenceTargetsContractTests : EvidenceTargetsContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override async Task<TargetsUnderTest> ArrangeAsync(string? repoFile, string? configFile)
    {
        var repo = _root.Combine("repo");
        _ = Directory.CreateDirectory(Path.Combine(repo, ".agentic-workflow"));
        if (repoFile is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(repo, ".agentic-workflow", "evidence.json"), repoFile, TestContext.Current.CancellationToken);
        }

        var configPath = _root.Combine("targets.json");
        if (configFile is not null)
        {
            await File.WriteAllTextAsync(configPath, configFile, TestContext.Current.CancellationToken);
        }

        return new TargetsUnderTest(new FileEvidenceTargets(new FileRunFiles(), TestJson.Options, new FileExecutableLocator()), repo, configFile is null ? null : configPath);
    }

    public void Dispose() => _root.Dispose();
}
