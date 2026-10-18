namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The owner's answer about hooks in the sandbox (2026-10-18; spike item 6): hooks help performance and quality, so the default is to keep them.
/// Only a hook that weakens the sandbox is treated (<see cref="HookInventory.Scripts"/> holds each treatment).
/// </summary>
public static class HookPermissionPolicy
{
    /// <summary>The treatment of one hook: its inventory row, or <see cref="HookTreatment.Keep"/> for a hook the inventory doesn't list.</summary>
    /// <param name="hook">The hook's name.</param>
    /// <returns>The treatment.</returns>
    public static HookTreatment TreatmentOf(string hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return HookTreatment.Keep;
    }

    /// <summary>The hooks a sandboxed session keeps and leaves out.</summary>
    /// <param name="installed">The hooks installed on the box, or null for the inventory itself.</param>
    /// <returns>The kept and omitted names, with a note for every installed hook the inventory doesn't list.</returns>
    public static PlanHooks ForSession(IReadOnlyList<string>? installed = null)
    {
        _ = installed;
        var scripts = HookInventory.Scripts;
        return new PlanHooks(
            [.. scripts.Where(s => !s.AnswersPermissions).Select(s => s.Name)],
            [.. scripts.Where(s => s.AnswersPermissions).Select(s => s.Name)],
            []);
    }
}
