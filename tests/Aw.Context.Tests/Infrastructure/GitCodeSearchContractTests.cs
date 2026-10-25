using Aw.Context.Core;
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

    /// <summary>A query marked resolved goes straight to the search: the commit is not looked up again (an unknown one is then git's failure, 048, not 110).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_resolved_query_does_not_resolve_its_commit_again()
    {
        var search = CreateSearch(out var repo, out _);

        var result = await search.FilesNaming(new CodeSearchQuery(repo, new string('a', 40), ["menu"], Within: null, Max: 10) { Resolved = true }, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.ExtractionUnreadable.Code, result.Error.Code);
    }

    protected override void AddUncommitted(string path, string text)
    {
        var full = Path.Combine(_repos[^1].Path, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }
}
