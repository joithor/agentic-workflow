namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The closed inventory of the hooks that run in a session and the state paths they need (sandbox spec §3.1).</summary>
public static class HookInventory
{
    /// <summary>The state-directory exceptions.</summary>
    public static IReadOnlyList<StatePathEntry> StatePaths => throw new NotSupportedException();

    /// <summary>The installed hook scripts and whether each answers permissions.</summary>
    public static IReadOnlyList<HookScript> Scripts => throw new NotSupportedException();

    /// <summary>The hooks a sandboxed session keeps and omits, by <see cref="HookPermissionPolicy.OmitPermissionAnsweringHooks"/>.</summary>
    /// <returns>The two lists.</returns>
    public static PlanHooks ForSession() => throw new NotSupportedException();
}
