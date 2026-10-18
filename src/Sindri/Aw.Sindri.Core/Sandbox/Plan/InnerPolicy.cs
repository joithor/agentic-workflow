namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The inner policy of a plan: advice with a good error, not the boundary (sandbox spec SB2, §5).</summary>
/// <param name="Mode">Refuse or ask.</param>
/// <param name="Rules">The rules, in order.</param>
public sealed record InnerPolicy(InnerMode Mode, IReadOnlyList<InnerRule> Rules)
{
    /// <summary>The 'permissions.defaultMode' the rendered settings must set explicitly: the owner's settings may say 'bypassPermissions' (spike items 1 and 6).</summary>
    public string DefaultMode
    {
        get
        {
            _ = Mode;
            throw new NotSupportedException();
        }
    }

    /// <summary>Whether the rendered settings also set 'disableBypassPermissionsMode' to 'disable'.</summary>
    public bool DisableBypassPermissions
    {
        get
        {
            _ = Mode;
            throw new NotSupportedException();
        }
    }
}
