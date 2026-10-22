using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The setup-once catalog rows (spec §7.4): listed with their class, route and CLI form while the units that serve them are pending; ApproveAllRecipes and the profile rows are already there.</summary>
public sealed class SetupRowTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("AddRepo", "POST", "/setup/repos", "sindri repo add <path>", "pending:SO-A3")]
    [InlineData("RegisterMcp", "POST", "/setup/mcp", "sindri mcp register --apply", "pending:SO-A4")]
    [InlineData("ConnectOnyx", "POST", "/onyx/account", "sindri onyx connect", "ready")]
    [InlineData("DeferOnyx", "POST", "/onyx/defer", "sindri onyx defer", "ready")]
    public void Each_desk_action_has_a_row_with_its_route_and_cli_form(string action, string method, string route, string cli, string ui)
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == action);

        Assert.Equal((ActionClass.Desk, method, "/sindri" + route, cli, ui, false, false), (row.Class, row.Method, row.Route, row.Cli.Text, row.Ui, row.NeedsTerminal, row.Destructive));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_Onyx_setup_is_a_ready_job_row()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "SetupOnyx");

        Assert.Equal((ActionClass.Job, "POST", "/sindri/onyx/setup", "sindri onyx up", "ready"), (row.Class, row.Method, row.Route, row.Cli.Text, row.Ui));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_automatic_mcp_runs_are_the_setup_services_to_start()
    {
        var row = Assert.Single(new CommandCatalog().Rows, r => r.Action.Value == "RegisterMcp");

        Assert.Equal(["setup"], row.ServiceGrants);
    }
}
