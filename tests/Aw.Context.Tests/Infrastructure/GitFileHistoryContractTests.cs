using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.History;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GitFileHistoryContractTests : FileHistoryContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];
    private readonly List<string> _folders = [];

    public void Dispose()
    {
        _repos.ForEach(r => r.Dispose());
        _folders.ForEach(f => Directory.Delete(f, recursive: true));
    }

    protected override IFileHistory Create(out ContextRepo repo, out HistoryWorld world)
    {
        var temp = new TempGitRepo(HistoryWorlds.InitialFiles);
        _repos.Add(temp);
        repo = temp.Repo;
        world = HistoryWorlds.Git(temp);
        return new GitFileHistory(new GitCli());
    }

    protected override IFileHistory CreateShallow(out ContextRepo repo)
    {
        var temp = new TempGitRepo(HistoryWorlds.InitialFiles);
        _repos.Add(temp);
        _ = HistoryWorlds.Git(temp);
        var folder = Directory.CreateTempSubdirectory("aw-ctx-shallow-").FullName;
        _folders.Add(folder);
        _ = temp.Run("clone", "-q", "--depth", "1", "file://" + temp.Path, folder);
        repo = new ContextRepo(temp.Repo.Name, folder);
        return new GitFileHistory(new GitCli());
    }

    protected override IFileHistory CreateMissing(out ContextRepo repo)
    {
        repo = new ContextRepo(ContextRepoName.From("nowhere"), Path.Combine(Path.GetTempPath(), "aw-no-such-checkout"));
        return new GitFileHistory(new GitCli());
    }

    protected override void DirtyWorkingTree(ContextRepo repo) => File.WriteAllText(Path.Combine(repo.Path, "a.txt"), "uncommitted\n");
}
