using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The one function behind the owner's rtk question.</summary>
public sealed class DenyRuleExpansionTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_rtk_forms_follow_the_denied_commands_when_the_policy_includes_them()
    {
        InnerRule[] rules =
        [
            new(InnerRuleEffect.Deny, "Bash", "git push:*"),
            new(InnerRuleEffect.Allow, "Read", string.Empty),
            new(InnerRuleEffect.Deny, "Bash", "gh:*"),
            new(InnerRuleEffect.Deny, "Read", "/secret/**"),
        ];

        var expanded = DenyRuleExpansion.Expand(rules);

        var include = HookPermissionPolicy.IncludeRtkForms;
        if (include)
        {
            Assert.Equal(
                [rules[0], new(InnerRuleEffect.Deny, "Bash", "rtk git push:*"), new(InnerRuleEffect.Deny, "Bash", "*git push*"), rules[1], rules[2], new(InnerRuleEffect.Deny, "Bash", "rtk gh:*"), rules[3]],
                expanded);
        }
        else
        {
            Assert.Equal(rules, expanded);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Allow_rules_and_non_command_denies_are_never_expanded()
    {
        InnerRule[] rules = [new(InnerRuleEffect.Allow, "Bash", "git status:*"), new(InnerRuleEffect.Deny, "Read", "/x/**")];

        Assert.Equal(rules, DenyRuleExpansion.Expand(rules));
    }
}
