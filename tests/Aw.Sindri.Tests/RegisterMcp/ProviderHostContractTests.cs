using System.Diagnostics;
using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Infrastructure.Mcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RegisterMcp;

/// <summary>The real host over a scratch directory (never the owner's home): files, git's tracked set, PATH lookups, the config paths.</summary>
[Lane(TestLane.Integration)]
public sealed class ProviderHostContractTests : ProviderHostContract, IDisposable
{
    private readonly DirectoryInfo _scratch = Directory.CreateTempSubdirectory("provider-host-");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    protected override string Root => _scratch.FullName;

    public void Dispose() => _scratch.Delete(recursive: true);

    [Fact]
    public async Task A_write_leaves_no_temporary_file_behind()
    {
        var path = Path.Combine(Root, ".codex", "config.toml");

        await CreateHost().Write(path, "a = 1\n", Token);

        Assert.Equal([path], Directory.GetFiles(Path.GetDirectoryName(path)!));   // why: the path has a directory
    }

    [Fact]
    public async Task A_write_through_a_symlink_changes_the_link_target_and_keeps_the_link()
    {
        var target = Path.Combine(Directory.CreateDirectory(Path.Combine(Root, "dotfiles")).FullName, "config.toml");
        await File.WriteAllTextAsync(target, "a = 1\n", Token);
        var link = Path.Combine(Directory.CreateDirectory(Path.Combine(Root, ".codex")).FullName, "config.toml");
        _ = File.CreateSymbolicLink(link, target);

        await CreateHost().Write(link, "a = 2\n", Token);

        Assert.Equal(target, new FileInfo(link).LinkTarget);
        Assert.Equal("a = 2\n", await File.ReadAllTextAsync(target, Token));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task A_write_keeps_the_file_mode()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes");
        var path = Path.Combine(Root, "config.toml");
        await File.WriteAllTextAsync(path, "a = 1\n", Token);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        await CreateHost().Write(path, "a = 2\n", Token);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public async Task A_directory_outside_any_repo_tracks_nothing()
    {
        await File.WriteAllTextAsync(Path.Combine(Root, "mcp.json"), "{}", Token);

        Assert.False(await CreateHost().Tracked(Root, "mcp.json", Token));
    }

    [Fact]
    public void Providers_are_found_on_PATH_and_Cursor_also_by_its_config_directory()
    {
        var bin = Directory.CreateDirectory(Path.Combine(Root, "bin")).FullName;
        var claude = Path.Combine(bin, "my-claude");
        File.WriteAllText(Path.Combine(bin, "codex"), string.Empty);
        File.WriteAllText(claude, string.Empty);
        var host = Host(new(StringComparer.Ordinal) { ["PATH"] = "relative:" + bin, ["AW_SINDRI_CLAUDE_BIN"] = claude });

        Assert.True(host.Installed(ProviderFile.Codex));
        Assert.True(host.Installed(ProviderFile.Claude));
        Assert.False(host.Installed(ProviderFile.Cursor));
        _ = Directory.CreateDirectory(Path.Combine(Root, "home", ".cursor"));
        Assert.True(host.Installed(ProviderFile.Cursor));
    }

    [Fact]
    public void CLAUDE_CONFIG_DIR_and_CODEX_HOME_move_the_user_configs_else_they_are_under_HOME()
    {
        var home = Path.Combine(Root, "home");
        var moved = Host(new(StringComparer.Ordinal) { ["CLAUDE_CONFIG_DIR"] = "/cfg/claude", ["CODEX_HOME"] = "/cfg/codex" });
        var plain = Host([]);

        Assert.Equal(("/cfg/claude/.claude.json", "/cfg/codex/config.toml"), (moved.UserConfigPath(ProviderFile.Claude), moved.UserConfigPath(ProviderFile.Codex)));
        Assert.Equal(
            (Path.Combine(home, ".claude.json"), Path.Combine(home, ".codex", "config.toml"), Path.Combine(home, ".cursor", "mcp.json")),
            (plain.UserConfigPath(ProviderFile.Claude), plain.UserConfigPath(ProviderFile.Codex), plain.UserConfigPath(ProviderFile.Cursor)));
    }

    protected override IProviderHost CreateHost() => Host([]);

    protected override async Task Commit(string relativePath, string text)
    {
        var path = Path.Combine(Root, relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: the path has a directory
        await File.WriteAllTextAsync(path, text, Token);
        await Git(IsolatedGit.Init());
        await Git("add", "--", relativePath);
        await Git([.. IsolatedGit.NoHooks, "-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "-m", "init"]);
    }

    private ProviderHost Host(Dictionary<string, string> environment)
    {
        _ = environment.TryAdd("HOME", Path.Combine(Root, "home"));
        return new ProviderHost(McpSettings.Read(name => environment.GetValueOrDefault(name)));
    }

    private async Task Git(params string[] args)
    {
        var start = new ProcessStartInfo("git", ["-C", Root, .. args]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var git = Process.Start(start)!;   // why: Start returns a process for a command it found
        var error = git.StandardError.ReadToEndAsync(Token);
        _ = await git.StandardOutput.ReadToEndAsync(Token);
        await git.WaitForExitAsync(Token);
        Assert.True(git.ExitCode == 0, $"git {string.Join(' ', args)}: {await error}");
    }
}
