using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The setup-once catalog rows (spec §7.4): listed with their class, route and CLI form while the units that serve them are pending; ApproveAllRecipes and the profile rows are already there.</summary>
public sealed class SetupRowTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("AddRepo", "POST", "/setup/repos", "sindri repo add <path>", "pending:SO-A3")]
    [InlineData("RegisterMcp", "POST", "/setup/mcp", "sindri mcp register --apply", "pending:SO-A4")]
    [InlineData("SetupOnyx", "POST", "/onyx/setup", "sindri onyx up", "pending:SO-A5")]
    [InlineData("ConnectOnyx", "POST", "/onyx/account", "sindri onyx connect", "pending:SO-A5")]
    [InlineData("DeferOnyx", "POST", "/onyx/defer", "sindri onyx defer", "pending:SO-A5")]
    public void Each_desk_action_has_a_pending_row_with_its_route_and_cli_form(string action, string method, string route, string cli, string ui)
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == action);

        Assert.Equal((ActionClass.Desk, method, "/sindri" + route, cli, ui, false, false), (row.Class, row.Method, row.Route, row.Cli.Text, row.Ui, row.NeedsTerminal, row.Destructive));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_automatic_mcp_runs_are_the_setup_services_to_start()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "RegisterMcp");

        Assert.Equal(["setup"], row.ServiceGrants);
    }
}
