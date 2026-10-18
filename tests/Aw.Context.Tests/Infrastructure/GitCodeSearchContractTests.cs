using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Code;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GitCodeSearchContractTests : CodeSearchContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose() => _repos.ForEach(r => r.Dispose());

    protected override ICodeSearch CreateSearch(out ContextRepo repo, out string commit)
    {
        var temp = new TempGitRepo(Committed);
        _repos.Add(temp);
        repo = temp.Repo;
        commit = temp.Head;
        return new GitCodeSearch(new GitCli());
    }

    protected override void AddUncommitted(string path, string text)
    {
        var full = Path.Combine(_repos[^1].Path, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }
}
