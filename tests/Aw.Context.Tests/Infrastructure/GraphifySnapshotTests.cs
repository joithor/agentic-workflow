using System.Formats.Tar;
using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GraphifySnapshotTests : IDisposable
{
    private const int Kb = 1024;
    private readonly string _snapshot = Directory.CreateTempSubdirectory("aw-graphify-snap-").FullName;
    private readonly ContextRepo _repo = new(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes");

    public void Dispose() => Directory.Delete(_snapshot, recursive: true);

    private static async Task<MemoryStream> Tar(params (string Path, int Size)[] files)
    {
        var tar = new MemoryStream();
        var writer = new TarWriter(tar, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            foreach (var (path, size) in files)
            {
                var data = size == 0 ? Encoding.UTF8.GetBytes("text of " + path) : new byte[size];
                await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, path) { DataStream = new MemoryStream(data) }, TestContext.Current.CancellationToken);
            }
        }

        tar.Position = 0;
        return tar;
    }

    private static async Task<MemoryStream> Tar(params string[] paths) => await Tar([.. paths.Select(p => (p, 0))]);

    private async Task<GraphifySnapshot.Tally> Unpack(MemoryStream tar, ClusterLimits limits, ContextRepo? repo = null)
    {
        var tally = new GraphifySnapshot.Tally(limits);
        await GraphifySnapshot.Unpack(tar, _snapshot, repo ?? _repo, tally, TestContext.Current.CancellationToken);
        return tally;
    }

    private IEnumerable<string> Present() =>
        Directory.EnumerateFiles(_snapshot, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(_snapshot, f).Replace('\\', '/')).Order(StringComparer.Ordinal);

    [Fact]
    public async Task The_snapshot_graphify_reads_holds_no_path_the_repo_denies()
    {
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };
        await using var tar = await Tar("src/app.ts", ".env", "config/secrets/token.txt", "certs/server.pem", "private/notes.ts", "docs/readme.md");

        _ = await Unpack(tar, ClusterLimits.Default, repo);

        Assert.Equal(["docs/readme.md", "src/app.ts"], Present(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Only_bytes_actually_copied_count_toward_the_snapshot_total()
    {
        var limits = new ClusterLimits { MaxSnapshotMB = 1 };
        await using var tar = await Tar(("a.ts", 700 * Kb), ("b.ts", 700 * Kb), ("c.ts", 100 * Kb), ("d.ts", 100 * Kb));

        var tally = await Unpack(tar, limits);

        Assert.Equal(["a.ts", "c.ts", "d.ts"], Present(), StringComparer.Ordinal);   // b was skipped, so it used up no room: c and d still fit
        Assert.Equal((1, 0, 900L * Kb), (tally.OverSnapshot, tally.OverFile, tally.Copied));
    }

    [Fact]
    public async Task A_denied_or_oversized_file_does_not_use_up_the_snapshot_cap()
    {
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };
        var limits = new ClusterLimits { MaxSnapshotMB = 1, MaxFileMB = 1 };
        await using var tar = await Tar(("private/big.ts", 900 * Kb), ("huge.bin", 1536 * Kb), ("a.ts", 900 * Kb));

        var tally = await Unpack(tar, limits, repo);

        Assert.Equal(["a.ts"], Present(), StringComparer.Ordinal);
        Assert.Equal((0, 1), (tally.OverSnapshot, tally.OverFile));
    }

    [Fact]
    public async Task Zero_limits_copy_everything()
    {
        var limits = new ClusterLimits { MaxSnapshotMB = 0, MaxFileMB = 0 };
        await using var tar = await Tar(("a.ts", 700 * Kb), ("b.ts", 1536 * Kb), ("c.ts", 700 * Kb));

        var tally = await Unpack(tar, limits);

        Assert.Equal(["a.ts", "b.ts", "c.ts"], Present(), StringComparer.Ordinal);
        Assert.Equal((0, 0), (tally.OverSnapshot, tally.OverFile));
    }
}
