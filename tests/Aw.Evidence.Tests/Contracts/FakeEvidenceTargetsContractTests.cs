using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeEvidenceTargetsContractTests : EvidenceTargetsContract
{
    private const string Repo = "/work/repo";

    protected override Task<TargetsUnderTest> ArrangeAsync(string? repoFile, string? configFile)
    {
        var fake = new FakeEvidenceTargets(TestJson.Options);
        if (repoFile is not null)
        {
            _ = fake.File(Repo + "/.agentic-workflow/evidence.json", repoFile);
        }

        if (configFile is not null)
        {
            _ = fake.File("/work/targets.json", configFile);
        }

        return Task.FromResult(new TargetsUnderTest(fake, Repo, configFile is null ? null : "/work/targets.json"));
    }
}
