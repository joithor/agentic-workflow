using System.Globalization;
using System.Text;
using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Forge.Infrastructure.Git;

/// <summary>
/// Pushes one explicit sha with a lease, hardened against the repository it comes from (spec 10.2). The push runs from a
/// fresh empty git directory that borrows the repository's objects, so nothing in the repository's own config (a
/// credential helper, core.sshCommand, url.*.insteadOf, hooks) reaches git; git's system and global config are off, hooks
/// are off, --no-verify is set, and the credential travels in the environment, not in an argument.
/// </summary>
internal sealed class GitPusher(IRedactor redactor, string gitProgram = "git")
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    internal async Task<Result<PushOutcome>> Push(PushRequest request, string? authorizationHeader, CancellationToken cancellationToken)
    {
        var invalid = Check(request);
        if (invalid is not null)
        {
            return invalid;
        }

        var scratch = Path.Combine(Path.GetTempPath(), "aw-forge-push-" + Guid.NewGuid().ToString("N"));
        try
        {
            return await PushFrom(scratch, request, authorizationHeader, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteQuietly(scratch);
        }
    }

    private static Error? Check(PushRequest request)
    {
        if (!IsSha(request.Sha) || (request.LeaseSha is not null && !IsSha(request.LeaseSha)))
        {
            return ForgeErrors.Invalid.With("sha: 40 or 64 hexadecimal characters");
        }

        if (request.Branch.Length == 0 || request.Branch.StartsWith('-') || request.Branch.Contains("..", StringComparison.Ordinal)
            || request.Branch.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-')))
        {
            return ForgeErrors.Invalid.With("branch: letters, digits, '/', '.', '_' and '-' only");
        }

        if (request.Remote.Length == 0 || request.Remote.StartsWith('-'))
        {
            return ForgeErrors.Invalid.With("remote: a URL or a path");
        }

        return Directory.Exists(request.RepoDir) ? null : ForgeErrors.Invalid.With("repoDir: not a directory");
    }

    private static bool IsSha(string value) => value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);

    private async Task<Result<PushOutcome>> PushFrom(string scratch, PushRequest request, string? authorizationHeader, CancellationToken cancellationToken)
    {
        var baseEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GIT_ASKPASS"] = "true",
            ["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes",
        };

        var objects = await ObjectsDirectory(request.RepoDir, baseEnvironment, cancellationToken).ConfigureAwait(false);
        if (objects.IsError)
        {
            return objects.Error;
        }

        var init = await Git(["init", "--quiet", "--bare", scratch], baseEnvironment, null, cancellationToken).ConfigureAwait(false);
        if (init is not { ExitCode: 0 })
        {
            return ForgeErrors.PushFailed.With("git init failed");
        }

        var environment = new Dictionary<string, string>(baseEnvironment, StringComparer.Ordinal)
        {
            ["GIT_DIR"] = scratch,
            ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = objects.Value,
        };
        AddHeader(environment, authorizationHeader);

        var has = await Git(["cat-file", "-e", request.Sha + "^{commit}"], environment, null, cancellationToken).ConfigureAwait(false);
        if (has is not { ExitCode: 0 })
        {
            return ForgeErrors.Invalid.With("sha: not a commit in the repository");
        }

        var lease = string.Create(CultureInfo.InvariantCulture, $"--force-with-lease=refs/heads/{request.Branch}:{request.LeaseSha}");
        var pushed = await Git(
            [
                "-c", "core.hooksPath=" + (OperatingSystem.IsWindows() ? "NUL" : "/dev/null"),
                "-c", "credential.helper=",
                "-c", "protocol.ext.allow=never",
                "push", "--no-verify", "--no-recurse-submodules", "--porcelain", lease,
                "--", request.Remote, $"{request.Sha}:refs/heads/{request.Branch}",
            ],
            environment,
            null,
            cancellationToken).ConfigureAwait(false);
        return Outcome(pushed);
    }

    private Result<PushOutcome> Outcome(ProcessResult? pushed)
    {
        if (pushed is null)
        {
            return ForgeErrors.PushFailed.With("git could not be started");
        }

        if (pushed.ExitCode == 0)
        {
            return PushOutcome.Pushed;
        }

        var text = pushed.Output + "\n" + pushed.Error;
        if (text.Contains("stale info", StringComparison.Ordinal))
        {
            return PushOutcome.HeadMoved;
        }

        var tail = redactor.Scrub(text.Trim()).Text;
        return ForgeErrors.PushFailed.With(tail.Length > 400 ? tail[^400..] : tail);
    }

    private async Task<Result<string>> ObjectsDirectory(string repoDir, Dictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var common = await Git(["-C", repoDir, "rev-parse", "--path-format=absolute", "--git-common-dir"], environment, null, cancellationToken).ConfigureAwait(false);
        if (common is not { ExitCode: 0 })
        {
            return ForgeErrors.Invalid.With("repoDir: not a git repository");
        }

        return Path.Combine(common.Output.Trim(), "objects");
    }

    private Task<ProcessResult?> Git(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string? cwd, CancellationToken cancellationToken) =>
        ProcessRunner.Run(gitProgram, arguments, environment, cwd, Timeout, cancellationToken);

    private static void AddHeader(Dictionary<string, string> environment, string? header)
    {
        if (header is null)
        {
            return;
        }

        environment["GIT_CONFIG_COUNT"] = "1";
        environment["GIT_CONFIG_KEY_0"] = "http.extraHeader";
        environment["GIT_CONFIG_VALUE_0"] = header;
    }

    /// <summary>The HTTP basic header GitHub accepts for a token over git's smart-HTTP transport.</summary>
    internal static string BasicHeader(string token) =>
        "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + token));

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A scratch directory under the temp root: the next boot's cleanup takes it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }
}
