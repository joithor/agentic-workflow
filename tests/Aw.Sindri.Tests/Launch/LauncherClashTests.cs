using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A launch whose session name is already taken (#295 review run 3, I32): the live session and its hash are never touched, whatever the host answered about it first.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherClashTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-clash-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly LaunchRequest _request = A.LaunchRequest().Build();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private LauncherOptions Options => new(_home);

    private string HashFile => LaunchSecrets.PathFor(Options.SecretsDirectory, _request.Launch);

    private string SecretFile => LaunchSecrets.SecretPathFor(Options.SecretsDirectory, _request.Launch);

    private Launcher Make() =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => _host, new FakeTerminalViewer(), Options, _clock) { ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null) });

    private async Task LiveSession()
    {
        var started = await _host.Start(new HostSession(LaunchSessionName.For(_request.Launch), _home, ["sleep", "60"], new Dictionary<string, string>(StringComparer.Ordinal)), TimeSpan.FromSeconds(4), CancellationToken.None);
        Expect.True(started.IsSuccess);
    }

    private void OldHash() => LaunchSecrets.Write(Options.SecretsDirectory, _request.Launch, "the-live-wraps-secret");

    [Fact]
    public async Task A_gone_session_with_a_hash_starts_fresh_and_replaces_the_hash()
    {
        OldHash();
        var before = await File.ReadAllTextAsync(HashFile, TestContext.Current.CancellationToken);

        var result = await Make().Launch(_request, CancellationToken.None);

        Expect.True(result is StartedLaunch);
        Expect.Equal(1, _host.Started.Count);
        Expect.True(before != await File.ReadAllTextAsync(HashFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_live_session_without_a_hash_fails_the_launch_and_leaves_the_session()
    {
        await LiveSession();
        _host.ExistsAnswersFalse = true;   // nothing was found, so the start clashes

        var result = await Make().Launch(_request, CancellationToken.None);

        Expect.True(result is FailedLaunch { Error.Kind: ErrorKind.Conflict });
        Expect.Equal(1, _host.LiveCount);
        Expect.True(!File.Exists(HashFile) && !File.Exists(SecretFile));   // the minted pair is removed: it proves nothing to the live wrap
    }

    [Fact]
    public async Task An_exists_that_misses_a_live_session_with_a_hash_still_answers_it_and_keeps_the_hash()
    {
        await LiveSession();
        OldHash();
        var before = await File.ReadAllTextAsync(HashFile, TestContext.Current.CancellationToken);
        _host.ExistsAnswersFalse = true;   // has-session timed out; the start then clashes

        var result = await Make().Launch(_request, CancellationToken.None);

        Expect.True(result is StartedLaunch { Viewer.Kind: ViewerOutcomeKind.Skipped });
        Expect.Equal(1, _host.LiveCount);
        Expect.Equal(before, await File.ReadAllTextAsync(HashFile, TestContext.Current.CancellationToken));   // the live wrap's heartbeats still check
        Expect.True(!File.Exists(SecretFile));
    }

    [Fact]
    public async Task A_live_session_whose_hash_was_swept_is_failed_without_a_stop()
    {
        await LiveSession();   // Exists says true, the hash file is not there

        var result = await Make().Launch(_request, CancellationToken.None);

        Expect.True(result is FailedLaunch { Error.Kind: ErrorKind.Conflict });
        Expect.Equal(1, _host.LiveCount);
        Expect.True(!File.Exists(HashFile) && !File.Exists(SecretFile));
    }
}
