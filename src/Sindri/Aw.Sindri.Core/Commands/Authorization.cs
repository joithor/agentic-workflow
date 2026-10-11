namespace Aw.Sindri.Core.Commands;

/// <summary>
/// The authorization table (API spec §4.2) as data: which caller may run which action class. Pure; the command chain's
/// AuthorizingService asks it, and the command list renders <see cref="CallersFor"/>.
/// </summary>
public static class Authorization
{
    /// <summary>Decides whether <paramref name="caller"/> may run <paramref name="row"/>, whose class is <paramref name="actionClass"/>.</summary>
    /// <param name="actionClass">The attempt's action class (the row's, after classification).</param>
    /// <param name="caller">The caller, or null when no caller context is set (always refused).</param>
    /// <param name="row">The catalog row: its service grants, terminal need, name and CLI form.</param>
    /// <returns>The decision.</returns>
    public static AuthorizationDecision Decide(ActionClass actionClass, CallerContext? caller, CommandRow row) =>
        throw new NotSupportedException("red: Decide");

    /// <summary>Who may run <paramref name="actionClass"/>: 'human+terminal', 'human', 'session', 'service' or 'service:&lt;name&gt;'.</summary>
    /// <param name="actionClass">The action class.</param>
    /// <param name="grants">The row's service grants (desk rows only).</param>
    /// <returns>The callers, in that order.</returns>
    public static IReadOnlyList<string> CallersFor(ActionClass actionClass, IReadOnlyList<string> grants) =>
        throw new NotSupportedException("red: CallersFor");
}
