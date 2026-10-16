using Aw.Kernel.Json;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// What only the file adapter can get wrong: permissions and partial files.
public sealed class FileOnyxWorkspaceTests : IDisposable
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "aw-onyx-ws-" + Guid.NewGuid().ToString("N"));
    private readonly FileOnyxWorkspace _workspace;
    private readonly OnyxPaths _paths;

    public FileOnyxWorkspaceTests()
    {
        _paths = OnyxPaths.For(_home);
        _workspace = new FileOnyxWorkspace(new FakeSindriHome(_home), AwJson.CreateOptions(SindriJsonContext.Default));
    }

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_protected_file_is_0600_and_its_directory_0700()
    {
        _ = _workspace.WriteIfChanged(_paths.Env, "A=1\n", secret: true);

        Assert.Equal(OwnerFile, File.GetUnixFileMode(_paths.Env));
        Assert.Equal(OwnerDirectory, File.GetUnixFileMode(Path.GetDirectoryName(_paths.Env)!));
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void A_changed_protected_file_stays_0600()
    {
        _ = _workspace.WriteIfChanged(_paths.Env, "A=1\n", secret: true);

        _ = _workspace.WriteIfChanged(_paths.Env, "A=2\n", secret: true);

        Assert.Equal(OwnerFile, File.GetUnixFileMode(_paths.Env));
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void The_state_file_is_0600()
    {
        _workspace.WriteState(new OnyxSetupState(3000, new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Equal(OwnerFile, File.GetUnixFileMode(_paths.State));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_write_never_leaves_a_partial_file_for_a_reader()
    {
        var big = new string('x', 2_000_000);
        _ = _workspace.WriteIfChanged(_paths.Override, big + "a", secret: false);
        using var stop = new CancellationTokenSource();
        var lengths = new List<int>();
        var reader = Task.Run(
            () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    lengths.Add(_workspace.Read(_paths.Override)?.Length ?? -1);
                }
            },
            CancellationToken.None);

        for (var i = 0; i < 20; i++)
        {
            _ = _workspace.WriteIfChanged(_paths.Override, big + (i % 2 == 0 ? "b" : "c"), secret: false);
        }

        await stop.CancelAsync();
        await reader.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.All(lengths, static length => Assert.Equal(2_000_001, length));
    }

    [Fact, Trait("Lane", "integration")]
    public void No_temporary_file_is_left_behind()
    {
        _ = _workspace.WriteIfChanged(_paths.Override, "a\n", secret: false);
        _workspace.WriteState(new OnyxSetupState(3000, new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Empty(Directory.EnumerateFiles(_home, "*.tmp*", SearchOption.AllDirectories));
    }
}
