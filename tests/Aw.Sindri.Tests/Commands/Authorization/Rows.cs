using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Commands.Authorization;

/// <summary>Catalog rows for the authorization and chain tests: a command of a class, with what the table reads.</summary>
internal static class Rows
{
    /// <summary>A row for <paramref name="action"/> of <paramref name="actionClass"/>; none needs a terminal, as the catalog's protected rows no longer do (owner decision q93).</summary>
    public static CommandRow Of(ActionClass actionClass, string action = "FixtureCommand", Type? type = null, string cli = "sindri fixture <id>",
        params string[] grants) =>
        new(ActionId.From(action), type, action.Contains(':', StringComparison.Ordinal) ? action[(action.IndexOf(':', StringComparison.Ordinal) + 1)..] : null,
            actionClass, grants, NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false,
            Shortcut: null, "POST", "/sindri/fixture", CliForm.Parse(cli).Value, "ready");
}
