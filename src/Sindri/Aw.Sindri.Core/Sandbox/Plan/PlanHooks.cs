namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Which installed hooks run in the session.</summary>
/// <param name="Kept">Hook script names that run.</param>
/// <param name="Omitted">Hook script names left out of the session's settings (see <see cref="HookPermissionPolicy"/>).</param>
public sealed record PlanHooks(IReadOnlyList<string> Kept, IReadOnlyList<string> Omitted);
