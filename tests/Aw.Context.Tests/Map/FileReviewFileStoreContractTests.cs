using Aw.Context.Core.Review;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Review;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Review;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "integration")]
public sealed class FileReviewFileStoreContractTests : ReviewFileStoreContract, IDisposable
{
    private readonly TempHome _home = new();

    public FileReviewFileStoreContractTests()
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Root, "repo", ".git"));
    }

    protected override string Folder => Path.Combine(RealPath.Of(_home.Root)!, "context", "exports");   // why: the temp root exists, so it resolves

    protected override string Worktree => Path.Combine(_home.Root, "repo");

    public void Dispose() => _home.Dispose();

    protected override IReviewFileStore CreateStore() => new FileReviewFileStore(SettingsFromEnvironment.Read(_home.Environment), FixtureMap.Json);

    [Fact, Trait("Lane", "integration")]
    public async Task A_written_file_is_0600_in_a_0700_folder_with_no_temp_file_left()
    {
        var path = (await CreateStore().Write(path: null, "review-v14.json", new ReviewFile("v14@7c3e1a2", []), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(path, Assert.Single(Directory.EnumerateFiles(Folder)));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Folder));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_yaml_file_with_decisions_reads_and_a_broken_one_is_031()
    {
        var store = CreateStore();
        var yaml = Path.Combine(_home.Root, "decided.yaml");
        await File.WriteAllTextAsync(yaml, string.Join('\n',
            "draft: v14@7c3e1a2", "entries:",
            "  - node: feature:tabs.groups", "    label: Tab groups", "    group: model", "    confidence: 0.62", "    decision: rename",
            "  - link: edge:0123456789abcdef", "    label: TabManager.ts implements Tab manager", "    group: model", "    confidence: 0.74", "    decision: accept"), TestContext.Current.CancellationToken);
        var broken = Path.Combine(_home.Root, "broken.yaml");
        await File.WriteAllTextAsync(broken, "draft: [unclosed", TestContext.Current.CancellationToken);
        var unknownField = Path.Combine(_home.Root, "extra.json");
        await File.WriteAllTextAsync(unknownField, """{"draft":"v1","entries":[],"surprise":1}""", TestContext.Current.CancellationToken);

        var read = await store.Read(yaml, TestContext.Current.CancellationToken);
        var refused = await store.Read(broken, TestContext.Current.CancellationToken);
        var extra = await store.Read(unknownField, TestContext.Current.CancellationToken);

        Assert.Equal([ReviewVerb.Rename, ReviewVerb.Accept], read.Value.Entries.Select(e => e.Decision!.Value));
        Assert.Equal("SND-CONTEXT-031", refused.Error.Code);
        Assert.Equal("SND-CONTEXT-031", extra.Error.Code);
    }
}
