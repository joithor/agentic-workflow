using System.Collections;
using System.Diagnostics;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Processes;

/// <summary>
/// The child environment (deployable-context spec A2d): on the desktop a child inherits as it always did; in pod mode
/// (AW_SINDRI_DEPLOY=pod) it gets an allowlist plus the one credential a call passes explicitly, never the pod's other secrets.
/// </summary>
public sealed class ChildEnvironmentTests
{
    private static Dictionary<string, string> Parent(params (string Name, string Value)[] variables) =>
        variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);

    private static Dictionary<string, string> PodParent() => Parent(
        ("AW_SINDRI_DEPLOY", "pod"), ("PATH", "/usr/bin"), ("HOME", "/data/home"), ("LANG", "C.UTF-8"), ("LC_ALL", "C"), ("TMPDIR", "/tmp"),
        ("AW_STATE_DIR", "/data/state"), ("AW_SINDRI_HOME", "/data/state/sindri-app"), ("DOTNET_EnableDiagnostics", "0"),
        ("AW_FORGE_TOKEN", "forge-secret"), ("GITHUB_TOKEN", "gh-secret"), ("AWS_SECRET_ACCESS_KEY", "aws-secret"), ("KUBERNETES_SERVICE_HOST", "10.0.0.1"));

    private static Dictionary<string, string?> Env(ProcessStartInfo start) =>
        start.Environment.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Desktop_mode_keeps_the_inherited_environment_and_applies_the_extras()
    {
        var inherited = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string?)e.Value, StringComparer.Ordinal);
        var extra = new Dictionary<string, string?>(StringComparer.Ordinal) { ["LC_ALL"] = "C", ["GIT_TERMINAL_PROMPT"] = "0" };

        var start = ChildEnvironment.Build("git", ["status"], extra, Parent(("PATH", "/usr/bin")));

        var env = Env(start);
        Assert.All(inherited.Where(p => p.Key is not ("LC_ALL" or "GIT_TERMINAL_PROMPT")), p => Assert.Equal(p.Value, env[p.Key]));
        Assert.Equal("C", env["LC_ALL"]);
        Assert.Equal("0", env["GIT_TERMINAL_PROMPT"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_null_extra_removes_the_variable_in_both_modes()
    {
        var removal = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = null };

        Assert.DoesNotContain("PATH", Env(ChildEnvironment.Build("x", null, removal, Parent())).Keys);
        Assert.DoesNotContain("PATH", Env(ChildEnvironment.Build("x", null, removal, PodParent())).Keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_file_and_arguments_are_set_and_no_shell_is_used()
    {
        var start = ChildEnvironment.Build("git", ["-C", "/repo", "status"], null, Parent());

        Assert.Equal("git", start.FileName);
        Assert.Equal(["-C", "/repo", "status"], start.ArgumentList);
        Assert.False(start.UseShellExecute);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pod_mode_passes_the_allowlist_and_nothing_else()
    {
        var env = Env(ChildEnvironment.Build("git", ["status"], null, PodParent()));

        Assert.Equal(
            ["AW_SINDRI_DEPLOY", "AW_SINDRI_HOME", "AW_STATE_DIR", "DOTNET_EnableDiagnostics", "HOME", "LANG", "LC_ALL", "PATH", "TMPDIR"],
            env.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("/data/home", env["HOME"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pod_mode_passes_the_one_credential_a_call_names()
    {
        var extra = new Dictionary<string, string?>(StringComparer.Ordinal) { ["GIT_CRED"] = "token-for-this-call", ["GIT_TERMINAL_PROMPT"] = "0" };

        var env = Env(ChildEnvironment.Build("git", ["fetch"], extra, PodParent()));

        Assert.Equal("token-for-this-call", env["GIT_CRED"]);
        Assert.Equal("0", env["GIT_TERMINAL_PROMPT"]);
        Assert.DoesNotContain("AW_FORGE_TOKEN", env.Keys);
        Assert.DoesNotContain("GITHUB_TOKEN", env.Keys);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("pod", true)]
    [InlineData("POD", true)]
    [InlineData("", false)]
    [InlineData("desktop", false)]
    public void Only_the_value_pod_selects_pod_mode(string value, bool pod)
    {
        // the parent's AW_STATE_DIR is a sentinel: an allowlisted variable reaches the child from the parent only in pod mode
        var env = Env(ChildEnvironment.Build("x", null, null, Parent(("AW_SINDRI_DEPLOY", value), ("AW_STATE_DIR", "/sentinel-state"))));

        Assert.Equal(pod, env.GetValueOrDefault("AW_STATE_DIR") == "/sentinel-state");
    }
}
