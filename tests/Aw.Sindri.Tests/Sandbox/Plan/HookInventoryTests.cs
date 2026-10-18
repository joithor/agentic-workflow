using System.Text.RegularExpressions;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The hook inventory against the hooks that are actually installed (sandbox spec §3.1): a hook that can't run fails open, so a drift here switches a guard off silently.</summary>
public sealed partial class HookInventoryTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
    }

    private static IReadOnlyList<string> HookFiles() =>
        [.. Directory.GetFiles(Path.Combine(RepoRoot(), "config", "hooks"), "*.sh", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public void Hook_inventory_matches_config_hooks()
    {
        var installed = HookFiles().Select(f => Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(installed, [.. HookInventory.Scripts.Select(s => s.Name).Order(StringComparer.Ordinal)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_permission_answering_column_matches_what_the_hooks_print()
    {
        foreach (var file in HookFiles())
        {
            var answers = AllowDecision.IsMatch(File.ReadAllText(file));
            var entry = HookInventory.Scripts.Single(s => string.Equals(s.Name, Path.GetFileNameWithoutExtension(file), StringComparison.Ordinal));

            Assert.True(entry.AnswersPermissions == answers, $"{entry.Name}: inventory says {entry.AnswersPermissions}, the script says {answers}");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_state_path_the_hooks_use_is_in_the_inventory()
    {
        var files = HookFiles().Append(Path.Combine(RepoRoot(), "config", "lib", "failopen.sh")).ToList();
        var used = files.SelectMany(f => StatePath.Matches(File.ReadAllText(f)).Select(m => m.Groups["p"].Value.TrimEnd('/', '"', '}'))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.NotEmpty(used);
        foreach (var path in used)
        {
            Assert.True(
                HookInventory.StatePaths.Any(e => string.Equals(path, e.Path, StringComparison.Ordinal) || path.StartsWith(e.Path + "/", StringComparison.Ordinal)),
                $"AW_STATE_DIR/{path} is used by a hook and is not in the inventory");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_inventory_binds_what_the_spec_names()
    {
        var kinds = HookInventory.StatePaths.ToDictionary(e => e.Path, e => e.Kind, StringComparer.Ordinal);

        Assert.Equal(StatePathKind.ReadWrite, kinds["judge"]);
        Assert.Equal(StatePathKind.ReadWrite, kinds["locks"]);
        Assert.Equal(StatePathKind.PerLaunchDir, kinds["logs"]);
        Assert.Equal(StatePathKind.PerLaunchDir, kinds["probe"]);
        Assert.Equal(StatePathKind.PerLaunchDir, kinds["digests"]);
        Assert.Equal(StatePathKind.ReadOnly, kinds["context-guard"]);
        Assert.Equal(StatePathKind.ReadWrite, kinds["context-guard/state"]);
        Assert.Equal(StatePathKind.ReadOnly, kinds["bin-r1b"]);
        Assert.Equal(StatePathKind.Absent, kinds["meta"]);
        Assert.Equal(StatePathKind.Absent, kinds["bin/aw-meta"]);
        Assert.All(HookInventory.StatePaths, e => Assert.False(string.IsNullOrWhiteSpace(e.UsedBy), e.Path));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_omits_the_permission_answering_hooks_when_the_policy_says_so()
    {
        var answering = HookInventory.Scripts.Where(s => s.AnswersPermissions).Select(s => s.Name).Order(StringComparer.Ordinal).ToList();

        var hooks = HookInventory.ForSession();

        Assert.Equal(["external-write-guard", "record-teammate-name", "rtk-rewrite", "scope-gate", "send-gate"], answering);
        Assert.Equal(HookPermissionPolicy.OmitPermissionAnsweringHooks ? answering : [], [.. hooks.Omitted.Order(StringComparer.Ordinal)]);
        Assert.Equal(HookInventory.Scripts.Count, hooks.Kept.Count + hooks.Omitted.Count);
        Assert.Empty(hooks.Kept.Intersect(hooks.Omitted, StringComparer.Ordinal));
    }

    [GeneratedRegex("permissionDecision\"?\\s*:\\s*\"allow\"", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AllowDecision { get; }

    [GeneratedRegex("(?:AW_STATE_DIR:-\\$HOME/\\.agentic-workflow\\}|\\$AW_STATE_DIR)/(?<p>[A-Za-z0-9_./-]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StatePath { get; }
}
