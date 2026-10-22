using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Infrastructure.LensPacks;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Tests.Workflows.Effects;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The git adapter of the changed paths against a real temp repo: a base commit and a head that adds two files and changes one.</summary>
[Lane(TestLane.Integration)]
public sealed class GitChangedPathsContractTests : ChangedPathsContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    protected override async ValueTask<(IChangedPaths Paths, string Checkout, string BaseRef, string Head)> Create()
    {
        var repo = await TempGitRepo.Create(commits: 0);
        _repos.Add(repo);
        var identity = new[] { "-c", "user.email=test@example.invalid", "-c", "user.name=test" };
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "README.md"), "one", TestContext.Current.CancellationToken);
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. identity, "commit", "-q", "-m", "base"]);
        var baseRef = await repo.Git("rev-parse", "HEAD");
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "README.md"), "two", TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Path.Combine(repo.Path, "src"));
        _ = Directory.CreateDirectory(Path.Combine(repo.Path, "docs", "guide"));
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "a.cs"), "a", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "docs", "guide", "b.md"), "b", TestContext.Current.CancellationToken);
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. identity, "commit", "-q", "-m", "head"]);
        return (new GitChangedPaths(), repo.Path, baseRef, await repo.Git("rev-parse", "HEAD"));
    }
}
