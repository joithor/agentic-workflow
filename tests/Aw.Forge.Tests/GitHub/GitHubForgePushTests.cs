using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Credentials;
using Aw.Forge.Infrastructure.Git;
using Aw.Forge.Infrastructure.GitHub;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Redaction;

namespace Aw.Forge.Tests.GitHub;

/// <summary>
/// GitHubForge.Push's happy path: the credential reaches git as an http.extraHeader in the environment, never in an
/// argument. A shim stands in for the git program and records what the push call was given.
/// </summary>
[Trait("Lane", "integration")]
public sealed class GitHubForgePushTests : IDisposable
{
    private const string Secret = "sekret-value-0004";
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("aw-forge-ghpush-");

    public void Dispose() => _dir.Delete(recursive: true);

    [Fact]
    public async Task A_push_hands_git_the_basic_header_through_the_environment_and_no_argument_carries_it()
    {
        var recorded = Path.Combine(_dir.FullName, "push.txt");
        var shim = Path.Combine(_dir.FullName, "git-shim.sh");
        _ = ExecutableScript.Write(
            shim,
            "case \"$*\" in\n"
            + "  *--git-common-dir*) echo \"" + _dir.FullName + "/common\" ;;\n"
            + "  *--no-verify*) { echo \"count=$GIT_CONFIG_COUNT\"; echo \"key=$GIT_CONFIG_KEY_0\"; echo \"value=$GIT_CONFIG_VALUE_0\"; echo \"args=$*\"; } > \"" + recorded + "\" ;;\n"
            + "esac\n"
            + "exit 0\n");
        var credentials = new StubCredentials(Secret);
        var forge = new GitHubForge(
            new GitHubApi(new HttpClient(new StubHandler()), credentials, new ForgeHosts(["github.com"])),
            credentials,
            new GitPusher(Redactor.Default, shim),
            Redactor.Default);
        var request = new PushRequest(_dir.FullName, new string('a', 40), "https://github.com/acme/widgets.git", "feature", LeaseSha: null);

        var result = await forge.Push(request, TestContext.Current.CancellationToken);

        Assert.Equal(PushOutcome.Pushed, result.Value);
        var seen = await File.ReadAllLinesAsync(recorded, TestContext.Current.CancellationToken);
        Assert.Contains("count=1", seen, StringComparer.Ordinal);
        Assert.Contains("key=http.extraHeader", seen, StringComparer.Ordinal);
        Assert.Contains("value=" + GitPusher.BasicHeader(Secret), seen, StringComparer.Ordinal);
        var arguments = seen.Single(l => l.StartsWith("args=", StringComparison.Ordinal));
        Assert.Contains("https://github.com/acme/widgets.git", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", arguments, StringComparison.Ordinal);
    }

    private sealed class StubCredentials(string token) : ICredentialSource
    {
        public ValueTask<Result<string>> Token(string host, bool refresh, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(token));
    }
}
