using System.Globalization;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>Writes a stub 'gh' that answers the calls GhRepoHost makes with a scenario's JSON (and HTTP errors the way
/// gh prints them: the body on stdout, "gh: … (HTTP 404)" on stderr, exit 1).</summary>
internal static class StubGh
{
    internal static string Write(string directory, HostScenario scenario, DateTimeOffset now)
    {
        var ready = scenario is HostScenario.Ready or HostScenario.Rulesets or HostScenario.NotAdmin;
        string Day(int days) => now.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var releases = ready
            ? $"[{{\"published_at\":\"{Day(1)}\"}},{{\"published_at\":\"{Day(30)}\"}},{{\"published_at\":\"{Day(60)}\"}},{{\"published_at\":\"{Day(200)}\"}},{{\"published_at\":null}}]"
            : "[]";
        var labels = ready
            ? "[{\"name\":\"type:bug\"},{\"name\":\"area:kernel\"},{\"name\":\"size:S\"},{\"name\":\"ring:0\"}]"
            : "[{\"name\":\"bug\"},{\"name\":\"documentation\"}]";
        var admin = scenario == HostScenario.NotAdmin ? "false" : "true";
        var status = ready ? "enabled" : "disabled";
        var scanning = scenario == HostScenario.NotAdmin
            ? "{\"permissions\":{\"admin\":false}}"
            : "{\"permissions\":{\"admin\":" + admin + "},\"security_and_analysis\":{\"secret_scanning\":{\"status\":\"" + status + "\"},\"secret_scanning_push_protection\":{\"status\":\"" + status + "\"}}}";
        var protection = Protection(scenario);
        // Rulesets: no branch protection (404) but ruleset rules apply to main.
        var rules = scenario == HostScenario.Rulesets ? "[{\"type\":\"pull_request\"},{\"type\":\"non_fast_forward\"}]" : "[]";
        string[] script =
        [
            "#!/bin/sh",
            "case \"$*\" in",
            "  \"repo view --json nameWithOwner,defaultBranchRef\") echo '{\"defaultBranchRef\":{\"name\":\"main\"},\"nameWithOwner\":\"owner/repo\"}' ;;",
            "  \"api repos/owner/repo/branches/main/protection\") " + protection + " ;;",
            "  \"api repos/owner/repo/rules/branches/main\") echo '" + rules + "' ;;",
            "  \"api repos/owner/repo\") echo '" + scanning + "' ;;",
            "  \"api repos/owner/repo/releases?per_page=100\") echo '" + releases + "' ;;",
            "  \"api repos/owner/repo/labels?per_page=100\") echo '" + labels + "' ;;",
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

    private static string Protection(HostScenario scenario) => scenario switch
    {
        HostScenario.Ready or HostScenario.NotAdmin => "echo '{\"url\":\"x\"}'",
        HostScenario.PlanCannotProtect =>
            "echo '{\"message\":\"Upgrade to GitHub Pro or make this repository public to enable this feature.\",\"status\":\"403\"}'; echo 'gh: Upgrade to GitHub Pro or make this repository public to enable this feature. (HTTP 403)' >&2; exit 1",
        HostScenario.Bare or HostScenario.Rulesets or HostScenario.Unreachable =>
            "echo '{\"message\":\"Branch not protected\",\"status\":\"404\"}'; echo 'gh: Branch not protected (HTTP 404)' >&2; exit 1",
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, message: null),
    };
}
