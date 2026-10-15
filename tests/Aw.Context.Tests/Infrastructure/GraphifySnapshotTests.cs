using System.Formats.Tar;
using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GraphifySnapshotTests : IDisposable
{
    private readonly string _snapshot = Directory.CreateTempSubdirectory("aw-graphify-snap-").FullName;

    public void Dispose() => Directory.Delete(_snapshot, recursive: true);

    private static async Task<MemoryStream> Tar(params string[] paths)
    {
        var tar = new MemoryStream();
        var writer = new TarWriter(tar, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            foreach (var path in paths)
            {
                await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, path) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes("text of " + path)) }, TestContext.Current.CancellationToken);
            }
        }

        tar.Position = 0;
        return tar;
    }

    [Fact]
    public async Task The_snapshot_graphify_reads_holds_no_path_the_repo_denies()
    {
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };
        await using var tar = await Tar("src/app.ts", ".env", "config/secrets/token.txt", "certs/server.pem", "private/notes.ts", "docs/readme.md");

        await GraphifyClusterSource.Unpack(tar, _snapshot, repo, TestContext.Current.CancellationToken);

        var present = Directory.EnumerateFiles(_snapshot, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(_snapshot, f).Replace('\\', '/')).Order(StringComparer.Ordinal);
        Assert.Equal(["docs/readme.md", "src/app.ts"], present, StringComparer.Ordinal);
    }
}
