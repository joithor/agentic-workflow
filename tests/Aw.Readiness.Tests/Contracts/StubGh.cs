using System.Globalization;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>Writes a stub 'gh' that answers the calls GhRepoHost makes with a scenario's JSON (and HTTP errors the way
/// gh prints them: the body on stdout, "gh: … (HTTP 404)" on stderr, exit 1).</summary>
internal static class StubGh
{
    internal static string Write(string directory, HostScenario scenario, DateTimeOffset now)
    {
        var ready = scenario == HostScenario.Ready;
        string Day(int days) => now.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var releases = ready
            ? $"[{{\"published_at\":\"{Day(1)}\"}},{{\"published_at\":\"{Day(30)}\"}},{{\"published_at\":\"{Day(60)}\"}},{{\"published_at\":\"{Day(200)}\"}},{{\"published_at\":null}}]"
            : "[]";
        var scanning = ready
            ? "{\"security_and_analysis\":{\"secret_scanning\":{\"status\":\"enabled\"},\"secret_scanning_push_protection\":{\"status\":\"enabled\"}}}"
            : "{\"security_and_analysis\":{\"secret_scanning\":{\"status\":\"disabled\"},\"secret_scanning_push_protection\":{\"status\":\"disabled\"}}}";
        var protection = ready
            ? "echo '{\"url\":\"x\"}'"
            : "echo '{\"message\":\"Branch not protected\",\"status\":\"404\"}'; echo 'gh: Branch not protected (HTTP 404)' >&2; exit 1";
        string[] script =
        [
            "#!/bin/sh",
            "case \"$*\" in",
            "  \"repo view --json nameWithOwner,defaultBranchRef\") echo '{\"defaultBranchRef\":{\"name\":\"main\"},\"nameWithOwner\":\"owner/repo\"}' ;;",
            "  \"api repos/owner/repo/branches/main/protection\") " + protection + " ;;",
            "  \"api repos/owner/repo/rules/branches/main\") echo '[]' ;;",
            "  \"api repos/owner/repo\") echo '" + scanning + "' ;;",
            "  \"api repos/owner/repo/releases?per_page=100\") echo '" + releases + "' ;;",
            "  *) echo \"stub gh: unexpected call: $*\" >&2; exit 2 ;;",
            "esac",
        ];
        var path = Path.Combine(directory, "gh");
        File.WriteAllText(path, string.Join('\n', script) + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return path;
    }
}
