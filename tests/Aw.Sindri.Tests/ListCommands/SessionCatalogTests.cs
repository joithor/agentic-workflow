using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.ListCommands;

/// <summary>The catalog and the session routes (collector spec CA-7): FocusSession is a pending desk row until CO-4a; the queries get none.</summary>
public sealed class SessionCatalogTests
{
    private static IReadOnlyList<CommandRow> Rows => new CommandCatalog().Rows;

    [Fact, Trait("Lane", "unit")]
    public void Catalog_lists_FocusSession_as_a_pending_desk_row_for_CO_4a()
    {
        var row = Assert.Single(Rows, r => r.Action.Value == "FocusSession");

        Assert.Equal((ActionClass.Desk, "pending:CO-4a", "POST", "/sindri/sessions/{provider}/{id}/focus", false), (row.Class, row.Ui, row.Method, row.Route, row.NeedsTerminal));
        Assert.Null(row.CommandType);
        Assert.Equal("sindri agents focus <ref>", row.Cli.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void Catalog_has_no_row_for_the_two_session_queries() =>
        Assert.DoesNotContain(Rows, r => new[] { "ListSessions", "GetSession", "GetSessionPreview" }.Contains(r.Action.Value, StringComparer.Ordinal));
}
