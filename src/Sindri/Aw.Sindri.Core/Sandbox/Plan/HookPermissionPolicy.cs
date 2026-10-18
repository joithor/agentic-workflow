namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The owner's open question about hooks that answer permissions (spike item 6): owner hooks print 'allow', which defeats 'dontAsk', and
/// rtk-rewrite turns 'git push' into 'rtk git push' before the rules are matched. The lead's proposal is the default here; the owner's
/// answer is a one-line change of these two constants.
/// </summary>
public static class HookPermissionPolicy
{
    /// <summary>True: a sandboxed session's settings leave out the hooks that answer permissions (see <see cref="HookInventory"/>).</summary>
    public const bool OmitPermissionAnsweringHooks = true;

    /// <summary>True: the deny rules also list the 'rtk' forms of every command they deny (see <see cref="DenyRuleExpansion"/>).</summary>
    public const bool IncludeRtkForms = true;
}
