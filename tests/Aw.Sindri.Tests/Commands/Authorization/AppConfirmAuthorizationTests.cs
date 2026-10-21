using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Testing;
using Table = Aw.Sindri.Core.Commands.Authorization;

namespace Aw.Sindri.Tests.Commands.Authorization;

/// <summary>The per-row AppConfirm flag (setup-once spec §4 item 7): the app passes the table on a flagged row and nowhere else; a session, a service and no caller never do.</summary>
public sealed class AppConfirmAuthorizationTests
{
    private static CommandRow Row(ActionClass actionClass, bool appConfirm) => Rows.Of(actionClass) with { AppConfirm = appConfirm };

    [Theory, Trait("Lane", "unit")]
    [InlineData(ActionClass.Protected)]
    [InlineData(ActionClass.Job)]
    public void The_app_passes_a_row_that_carries_the_flag_whatever_its_terminal_need(ActionClass actionClass)
    {
        var row = Row(actionClass, appConfirm: true) with { NeedsTerminal = true };

        Assert.True(Table.Decide(actionClass, A.Caller().Human().Build(), row).Allowed);
        Assert.True(Table.Decide(actionClass, A.Caller().AtTerminal().Build(), row).Allowed);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ActionClass.Protected)]
    [InlineData(ActionClass.Job)]
    public void The_app_is_refused_a_terminal_row_without_the_flag(ActionClass actionClass)
    {
        var row = Row(actionClass, appConfirm: false) with { NeedsTerminal = true };

        var decision = Table.Decide(actionClass, A.Caller().Human().Build(), row);

        Assert.Equal(SindriErrors.AuthRefused.Code, decision.Refusal?.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ActionClass.Protected)]
    [InlineData(ActionClass.Job)]
    public void A_session_a_service_and_no_caller_are_refused_on_a_flagged_row(ActionClass actionClass)
    {
        var row = Row(actionClass, appConfirm: true) with { NeedsTerminal = true };

        foreach (var caller in new[] { A.Caller().Session().Build(), A.Caller().Service("jobs").Build(), null })
        {
            Assert.Equal(SindriErrors.AuthRefused.Code, Table.Decide(actionClass, caller, row).Refusal?.Code);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_flagged_rows_refusal_points_at_the_terminal_or_the_app()
    {
        var row = Row(ActionClass.Protected, appConfirm: true);

        var refusal = Table.Decide(ActionClass.Protected, A.Caller().Session().Build(), row).Refusal!;

        Assert.Contains("in a terminal or the Sindri app", refusal.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_list_names_the_app_as_a_caller_of_a_flagged_row_only()
    {
        Assert.Equal(["human+terminal", "human"], Table.CallersFor(Row(ActionClass.Protected, appConfirm: true)));
        Assert.Equal(["human+terminal"], Table.CallersFor(Row(ActionClass.Protected, appConfirm: false)));
    }
}
