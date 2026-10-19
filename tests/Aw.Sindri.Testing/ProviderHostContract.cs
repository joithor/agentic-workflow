using Aw.Sindri.Core.Mcp;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IProviderHost"/> honours, run against the fake and the real host (over a scratch directory).</summary>
public abstract class ProviderHostContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A scratch directory the host's files live under (an existing directory for the real host).</summary>
    protected abstract string Root { get; }

    /// <summary>A host over <see cref="Root"/>.</summary>
    /// <returns>The host.</returns>
    protected abstract IProviderHost CreateHost();

    /// <summary>Makes <see cref="Root"/> a git checkout where <paramref name="relativePath"/> is committed with <paramref name="text"/>.</summary>
    /// <param name="relativePath">The file, relative to the root.</param>
    /// <param name="text">Its text.</param>
    /// <returns>A task.</returns>
    protected abstract Task Commit(string relativePath, string text);

    /// <summary>A missing file reads as null.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_file_reads_as_null() =>
        Expect.True(await CreateHost().Read(Path.Combine(Root, "nope", "mcp.json"), Token) is null);

    /// <summary>A write creates the missing directory, reads back whole, and a second write replaces the whole text.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_write_creates_its_directory_and_replaces_the_whole_file()
    {
        var host = CreateHost();
        var path = Path.Combine(Root, ".cursor", "mcp.json");

        await host.Write(path, "{\"a\": 1}\n", Token);
        await host.Write(path, "{}\n", Token);

        Expect.Equal("{}\n", await host.Read(path, Token));
    }

    /// <summary>A committed file is tracked; an untracked file in the same checkout and a missing directory are not.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Only_a_committed_file_is_tracked()
    {
        await Commit(".cursor/mcp.json", "{}\n");
        var host = CreateHost();
        await host.Write(Path.Combine(Root, ".codex", "config.toml"), "a = 1\n", Token);

        Expect.True(await host.Tracked(Root, ".cursor/mcp.json", Token));
        Expect.True(!await host.Tracked(Root, ".codex/config.toml", Token));
        Expect.True(!await host.Tracked(Path.Combine(Root, "missing"), ".cursor/mcp.json", Token));
    }

    /// <summary>Each provider's user config has its own absolute path.</summary>
    [Fact]
    public void Each_provider_has_its_own_absolute_user_config_path()
    {
        var host = CreateHost();
        string[] paths = [host.UserConfigPath(ProviderFile.Claude), host.UserConfigPath(ProviderFile.Codex), host.UserConfigPath(ProviderFile.Cursor)];

        Expect.Equal(3, paths.Distinct(StringComparer.Ordinal).Count());
        Expect.True(paths.All(Path.IsPathRooted));
    }
}
