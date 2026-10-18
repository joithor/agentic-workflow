using Aw.Kernel.Testing;
using Aw.Meta.Core.Ids;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DirectoryLaneFilesRecordSessionTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    [Fact]
    public async Task Recording_a_session_writes_the_id_owner_only_and_replaces_the_old_one()
    {
        var files = new DirectoryLaneFiles(_dir.Path);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "api"), "s-old\n", TestContext.Current.CancellationToken);

        var recorded = await files.RecordSession("api", SessionId.From("s-new"), A.T0, TestContext.Current.CancellationToken);

        Assert.True(recorded.IsSuccess);
        Assert.Equal("s-new\n", await File.ReadAllTextAsync(Path.Combine(_dir.Path, "api"), TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(_dir.Path, "api")));
        Assert.True((await files.RecordSession("../x", SessionId.From("s"), A.T0, TestContext.Current.CancellationToken)).IsError);
    }

    public void Dispose() => _dir.Dispose();
}
