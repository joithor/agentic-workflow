namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The one function that expands the deny rules with the 'rtk' forms (owner question; <see cref="HookPermissionPolicy.IncludeRtkForms"/>).</summary>
public static class DenyRuleExpansion
{
    /// <summary>Adds the rtk form of every denied Bash command when the policy says so.</summary>
    /// <param name="rules">The rules.</param>
    /// <returns>The rules, with the extra forms after the ones they follow.</returns>
    public static IReadOnlyList<InnerRule> Expand(IReadOnlyList<InnerRule> rules) => throw new NotSupportedException();
}
