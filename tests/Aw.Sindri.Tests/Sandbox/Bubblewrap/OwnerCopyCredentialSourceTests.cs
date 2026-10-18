using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>What only the owner-copy source does: the copy, its mode, its binds, the expiry margin and the cleanup (spike item 2).</summary>
[Lane(TestLane.Integration)]
public sealed class OwnerCopyCredentialSourceTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('e', 32));
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private static UnixFileMode Mode(string path) => OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(path);

    [Fact]
    public async Task The_copy_is_mode_0600_in_a_0700_directory_and_leaves_no_temp_file()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        var source = home.Source();

        var prepared = (await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        var directory = prepared.Handle.Value;
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(directory + "/.credentials.json"));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(directory + "/.claude.json"));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, Mode(directory));
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        Assert.Equal(await File.ReadAllTextAsync(home.Root + "/.claude/.credentials.json", TestContext.Current.CancellationToken), await File.ReadAllTextAsync(directory + "/.credentials.json", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_agent_home_is_read_only_the_copy_is_the_config_directory_and_the_login_file_is_never_bound()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);

        var prepared = (await home.Source().Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        var config = home.Root + "/.claude-run";
        Assert.Equal(new LoginMount(home.Root + "/.claude", home.Root + "/.claude", MountMode.ReadOnly), prepared.Mounts[0]);
        Assert.Equal(new LoginMount(prepared.Handle.Value, config, MountMode.ReadWrite), prepared.Mounts[1]);
        Assert.Contains(new LoginMount(home.Root + "/.claude/settings.json", config + "/settings.json", MountMode.ReadOnly), prepared.Mounts);
        Assert.Contains(new LoginMount(home.Root + "/.claude/hooks", config + "/hooks", MountMode.ReadOnly), prepared.Mounts);
        Assert.DoesNotContain(prepared.Mounts, m => m.SandboxPath.Contains("/projects", StringComparison.Ordinal) || m.SandboxPath.EndsWith("credentials.json", StringComparison.Ordinal));
        Assert.Equal(config, prepared.Environment["CLAUDE_CONFIG_DIR"]);
        Assert.Equal([config], prepared.DeniedReads);
    }

    [Fact]
    public async Task Codex_gets_its_home_as_a_copy_with_the_sessions_directory_shared()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);

        var prepared = (await home.Source().Prepare(AgentId.From("codex"), _launch, TestContext.Current.CancellationToken)).Value;

        var config = home.Root + "/.codex-run";
        Assert.Equal(config, prepared.Environment["CODEX_HOME"]);
        Assert.Contains(new LoginMount(home.Root + "/.codex/sessions", config + "/sessions", MountMode.ReadWrite), prepared.Mounts);
        Assert.Contains(new LoginMount(home.Root + "/.codex/config.toml", config + "/config.toml", MountMode.ReadOnly), prepared.Mounts);
        Assert.True(File.Exists(prepared.Handle.Value + "/auth.json"));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void A_login_that_expires_within_the_planned_time_and_margin_is_017_expired(int hours, bool expired)
    {
        using var home = new FakeOwnerHome(LoginState.Ready, TimeSpan.FromHours(hours));

        var status = home.Source().Probe(_claude);

        Assert.Equal(expired ? LoginState.Expired : LoginState.Ready, status.State);
        Assert.Equal(expired ? "login too close to expiry; open Claude Code once to refresh it" : null, status.Detail);
    }

    [Fact]
    public async Task Release_removes_the_copy_and_nothing_else_and_a_foreign_handle_is_ignored()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        var source = home.Source();
        var prepared = (await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        source.Release(new LoginHandle(home.Root + "/.claude"));
        Assert.True(File.Exists(home.Root + "/.claude/settings.json"));
        source.Release(prepared.Handle);

        Assert.False(Directory.Exists(prepared.Handle.Value));
        Assert.False(Directory.Exists(home.SindriHome + "/launches/" + _launch.Value));
        Assert.True(File.Exists(home.Root + "/.claude/.credentials.json"));
    }

    [Fact]
    public async Task The_crash_sweep_removes_the_copies_of_launches_with_no_live_scope()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        var source = home.Source();
        var live = LaunchId.From("LCH-" + new string('f', 32));
        var dead = (await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;
        var kept = (await source.Prepare(_claude, live, TestContext.Current.CancellationToken)).Value;

        var removed = source.SweepStale(id => string.Equals(id, live.Value, StringComparison.Ordinal));

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(dead.Handle.Value));
        Assert.True(Directory.Exists(kept.Handle.Value));
    }
}
