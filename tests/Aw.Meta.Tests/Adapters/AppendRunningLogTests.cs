using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Adapters;

public sealed class AppendRunningLogTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_path_that_cannot_be_written_is_AW_META_008()
    {
        var log = new AppendRunningLog(_ => ValueTask.FromResult(_dir.Path));

        var result = await log.Append("- NOTE: x", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.LogUnwritable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Concurrent_appends_never_split_a_line()
    {
        var path = Path.Combine(_dir.Path, "log.md");
        var log = new AppendRunningLog(_ => ValueTask.FromResult(path));
        var lines = Enumerable.Range(0, 40).Select(i => "- NOTE " + new string((char)('a' + (i % 26)), 200)).ToList();

        var results = await Task.WhenAll(lines.Select(line => log.Append(line, CancellationToken.None).AsTask()));

        Assert.All(results, r => Assert.True(r.IsSuccess, r.IsError ? r.Error.Message : string.Empty));

        Assert.Equal(lines.Order(StringComparer.Ordinal), (await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken)).Order(StringComparer.Ordinal));
    }

    public void Dispose() => _dir.Dispose();
}
