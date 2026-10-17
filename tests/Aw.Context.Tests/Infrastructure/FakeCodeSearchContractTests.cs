using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Code;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class FakeCodeSearchContractTests : CodeSearchContract
{
    protected override ICodeSearch CreateSearch(out ContextRepo repo, out string commit)
    {
        repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes");
        commit = "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567";
        return new FakeCodeSearch(repo, commit, Committed);
    }

    protected override void AddUncommitted(string path, string text)
    {
        // why: the fake holds only the commit, so there is no working tree to add to
    }
}
