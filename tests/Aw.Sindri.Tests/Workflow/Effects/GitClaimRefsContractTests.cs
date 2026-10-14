using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>The git adapter against a real temp repo with one commit.</summary>
[Lane(TestLane.Integration)]
public sealed class GitClaimRefsContractTests : ClaimRefsContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    protected override async ValueTask<(IClaimRefs Refs, string Repository)> Create()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return (new GitClaimRefs(), repo.Path);
    }
}
