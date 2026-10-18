namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The one function that expands the deny rules with the forms a kept rewriting hook turns commands into (<see cref="HookTreatment.KeepDenyRewrites"/>).</summary>
public static class DenyRuleExpansion
{
    private const string Prefix = ":*";

    /// <summary>Adds the rewritten form of every denied Bash command for each kept hook that rewrites commands.</summary>
    /// <param name="rules">The rules.</param>
    /// <param name="hooks">The session's hooks.</param>
    /// <returns>The rules, with the extra forms after the ones they follow.</returns>
    public static IReadOnlyList<InnerRule> Expand(IReadOnlyList<InnerRule> rules, PlanHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(hooks);
        var include = true;
        if (!include)
        {
            return rules;
        }

        List<InnerRule> expanded = [];
        foreach (var rule in rules)
        {
            expanded.Add(rule);
            if (rule.Effect != InnerRuleEffect.Deny || !string.Equals(rule.Tool, "Bash", StringComparison.Ordinal) || !rule.Pattern.EndsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            expanded.Add(rule with { Pattern = "rtk " + rule.Pattern });
            var command = rule.Pattern[..^Prefix.Length];
            if (command.Contains(' ', StringComparison.Ordinal))
            {
                expanded.Add(rule with { Pattern = "*" + command + "*" });
            }
        }

        return expanded;
    }
}
