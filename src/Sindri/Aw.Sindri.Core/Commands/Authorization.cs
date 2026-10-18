using Aw.Kernel;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// The authorization table (API spec §4.2) as data: which caller may run which action class. Pure; the command chain's
/// AuthorizingService asks it, and the command list renders <see cref="CallersFor"/>. A row that needs a terminal
/// narrows any class to a human at one; a desk row may name the in-process services it grants ('service:host' →
/// StopProcess). 'step-report' is a launched session's (its launch secret is checked by the service, API spec §8.1), and 'local' never runs through the API, so it refuses everyone.
/// </summary>
public static class Authorization
{
    private const string ServicePrefix = "service:";

    private static readonly Dictionary<ActionClass, Who> _table = new()
    {
        [ActionClass.Request] = Who.Terminal | Who.App | Who.Session | Who.Service,
        [ActionClass.StepReport] = Who.Session,
        [ActionClass.Pause] = Who.Terminal | Who.App | Who.Session | Who.Service,
        [ActionClass.Job] = Who.Terminal | Who.App | Who.Service,
        [ActionClass.Decision] = Who.Terminal | Who.App,
        [ActionClass.Desk] = Who.Terminal | Who.App,
        [ActionClass.Protected] = Who.Terminal,
        [ActionClass.Local] = Who.None,
        [ActionClass.Issue] = Who.Terminal | Who.App | Who.Session,
    };

    [Flags]
    private enum Who
    {
        None = 0,
        Terminal = 1,
        App = 2,
        Session = 4,
        Service = 8,
    }

    /// <summary>Decides whether <paramref name="caller"/> may run <paramref name="row"/>, whose class is <paramref name="actionClass"/>.</summary>
    /// <param name="actionClass">The attempt's action class (the row's, after classification).</param>
    /// <param name="caller">The caller, or null when no caller context is set (always refused).</param>
    /// <param name="row">The catalog row: its service grants, terminal need, name and CLI form.</param>
    /// <returns>The decision.</returns>
    public static AuthorizationDecision Decide(ActionClass actionClass, CallerContext? caller, CommandRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var who = WhoIs(caller);
        var allowed = ((Allowed(actionClass) & who) != Who.None || Granted(actionClass, caller, row))
            && (!row.NeedsTerminal || who == Who.Terminal);
        return new AuthorizationDecision(caller?.Actor ?? "none", allowed ? null : Refusal(row));
    }

    /// <summary>Who may run <paramref name="actionClass"/>: 'human+terminal', 'human', 'session', 'service' or 'service:&lt;name&gt;'.</summary>
    /// <param name="actionClass">The action class.</param>
    /// <param name="grants">The row's service grants (desk rows only).</param>
    /// <returns>The callers, in that order.</returns>
    public static IReadOnlyList<string> CallersFor(ActionClass actionClass, IReadOnlyList<string> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        var allowed = Allowed(actionClass);
        (Who Who, string Name)[] named = [(Who.Terminal, "human+terminal"), (Who.App, "human"), (Who.Session, "session"), (Who.Service, "service")];
        var granted = actionClass == ActionClass.Desk ? grants.Select(static g => ServicePrefix + g) : [];
        return [.. named.Where(n => allowed.HasFlag(n.Who)).Select(static n => n.Name), .. granted];
    }

    private static Who Allowed(ActionClass actionClass) => _table.GetValueOrDefault(actionClass, Who.None);

    private static Who WhoIs(CallerContext? caller) => caller switch
    {
        null => Who.None,
        { Class: CallerClass.Human, Terminal: true } => Who.Terminal,
        { Class: CallerClass.Human } => Who.App,
        { Class: CallerClass.Session } => Who.Session,
        { Class: CallerClass.Service } => Who.Service,
        _ => Who.None,
    };

    /// <summary>A service the desk row names: 'service:host' on StopProcess.</summary>
    private static bool Granted(ActionClass actionClass, CallerContext? caller, CommandRow row) =>
        actionClass == ActionClass.Desk
        && caller is { Class: CallerClass.Service, Actor: var actor }
        && actor.StartsWith(ServicePrefix, StringComparison.Ordinal)
        && row.ServiceGrants.Contains(actor[ServicePrefix.Length..], StringComparer.Ordinal);

    private static Error Refusal(CommandRow row)
    {
        var where = row.Class == ActionClass.Protected || row.NeedsTerminal ? "in a terminal" : "in a terminal or the Sindri app";
        return SindriErrors.AuthRefused.With($"{row.Action.Value} is a human decision. Ask the human to run it {where}: {row.Cli.Text}");
    }
}
