using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Builds the inner policy (sandbox spec §5): target-neutral allow and deny rules that SB-5a renders for each agent CLI.</summary>
internal static class PlanPolicy
{
    private static readonly string[] _readOnlyCommands = ["git status", "git diff", "git log", "git show", "sindri"];

    internal static InnerPolicy Build(PlanContext c)
    {
        var r = c.Request;
        var allows = r.Attended ? AttendedAllows() : StepAllows(c, r.Policy!);   // why: Build checks that a Step carries its policy
        return new InnerPolicy(r.Attended ? InnerMode.Ask : InnerMode.Refuse, [.. allows, .. DenyRuleExpansion.Expand(DenyRules(c))]);
    }

    private static List<InnerRule> DenyRules(PlanContext c)
    {
        var state = c.StateDir;
        List<InnerRule> rules = [];
        foreach (var path in ReadDenied(c))
        {
            rules.Add(new InnerRule(InnerRuleEffect.Deny, "Read", path + "/**"));
            rules.Add(new InnerRule(InnerRuleEffect.Deny, "Grep", path));
            rules.Add(new InnerRule(InnerRuleEffect.Deny, "Bash", $"cat {path}*"));
        }

        foreach (var pattern in new[] { "env", "printenv*", "git push:*", "gh:*", "rm -rf /*", $"rm * {state}/locks*", $"mv * {state}/locks*", $"rm * {state}/judge*", $"mv * {state}/judge*" })
        {
            rules.Add(new InnerRule(InnerRuleEffect.Deny, "Bash", pattern));
        }

        return rules;
    }

    private static IEnumerable<string> ReadDenied(PlanContext c) =>
        c.Denied().Concat(c.OwnerDenied()).Select(d => d.Path).Concat(c.Request.Login?.DeniedReads ?? []).Distinct(StringComparer.Ordinal);

    private static List<InnerRule> AttendedAllows() =>
        [Allow("Read", string.Empty), Allow("Glob", string.Empty), Allow("Grep", string.Empty), Ask("Bash"), Ask("Edit"), Ask("Write")];

    private static InnerRule Ask(string tool) => new(InnerRuleEffect.Ask, tool, string.Empty);

    private static List<InnerRule> StepAllows(PlanContext c, StepPolicy policy)
    {
        List<string> roots = c.WritablePath is { } worktree ? [worktree, c.Request.Paths.RunDirectory] : [c.Request.Paths.RunDirectory];
        List<InnerRule> allows = policy.AllowedTools.Count > 0 ? [.. policy.AllowedTools.SelectMany(t => FromTool(t, roots, c.WriteScope)).Distinct()] : [.. Defaults(c, roots)];
        allows.Add(Allow("mcp__sindri-step__*", string.Empty));
        return allows;
    }

    private static IEnumerable<InnerRule> Defaults(PlanContext c, List<string> roots)
    {
        yield return Allow("Read", string.Empty);
        yield return Allow("Glob", string.Empty);
        yield return Allow("Grep", string.Empty);
        foreach (var root in roots)
        {
            yield return Allow("Edit", root + "/**");
            yield return Allow("Write", root + "/**");
        }

        if (c.WriteScope)
        {
            yield return Allow("Bash", string.Empty);
            yield break;
        }

        foreach (var command in _readOnlyCommands)
        {
            yield return Allow("Bash", command + ":*");
        }
    }

    private static IEnumerable<InnerRule> FromTool(string tool, List<string> roots, bool writeScope)
    {
        var open = tool.IndexOf('(', StringComparison.Ordinal);
        var name = open > 0 && tool.EndsWith(')') ? tool[..open] : tool;
        var pattern = open > 0 && tool.EndsWith(')') ? tool[(open + 1)..^1] : string.Empty;
        if (string.Equals(name, "Bash", StringComparison.Ordinal) && !writeScope)
        {
            return _readOnlyCommands.Select(cmd => Allow("Bash", cmd + ":*"));   // a read-only Step only tightens: its Bash stays the read-only set
        }

        if (string.Equals(name, "Edit", StringComparison.Ordinal) || string.Equals(name, "Write", StringComparison.Ordinal))
        {
            return pattern.Length == 0
                ? roots.Select(root => Allow(name, root + "/**"))
                : roots.Where(root => pattern.StartsWith(root + "/", StringComparison.Ordinal)).Take(1).Select(_ => Allow(name, pattern));
        }

        return [Allow(name, pattern)];
    }

    private static InnerRule Allow(string tool, string pattern) => new(InnerRuleEffect.Allow, tool, pattern);
}
