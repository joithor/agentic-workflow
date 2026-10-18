namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The one function that expands the deny rules with the 'rtk' forms (owner question; <see cref="HookPermissionPolicy.IncludeRtkForms"/>).</summary>
public static class DenyRuleExpansion
{
    private const string Prefix = ":*";

    /// <summary>Adds the rtk form of every denied Bash command when the policy says so.</summary>
    /// <param name="rules">The rules.</param>
    /// <returns>The rules, with the extra forms after the ones they follow.</returns>
    public static IReadOnlyList<InnerRule> Expand(IReadOnlyList<InnerRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var include = HookPermissionPolicy.IncludeRtkForms;
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
