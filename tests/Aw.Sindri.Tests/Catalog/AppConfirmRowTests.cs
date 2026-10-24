using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Table = Aw.Sindri.Core.Commands.Authorization;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The three rows the app confirms (setup-once spec §4 item 7, §7.4), after owner decision q93: no row carries the flag or a confirm value, and the table lets the app, the terminal and a session run them.</summary>
public sealed class AppConfirmRowTests
{
    private static readonly string[] _formerlyConfirmable = ["ApproveProfile", "ApproveRecipe", "ApproveAllRecipes"];

    private static IReadOnlyList<CommandRow> Rows { get; } = new CommandCatalog().Rows;

    [Fact, Trait("Lane", "unit")]
    public void No_row_carries_the_flag_a_confirm_value_or_a_typed_confirmation_status()
    {
        Assert.All(Rows, r =>
        {
            Assert.False(r.AppConfirm, r.Action.Value);
            Assert.Null(r.Confirm);
            Assert.NotEqual("cli-only:typed-confirmation", r.Ui);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void The_three_former_rows_are_ready_in_the_app() =>
        Assert.All(_formerlyConfirmable, a => Assert.Equal("ready", Assert.Single(Rows, r => r.Action.Value == a).Ui));

    [Fact, Trait("Lane", "unit")]
    public void The_app_challenge_is_a_desk_row_for_a_human()
    {
        var row = Assert.Single(Rows, r => r.Action.Value == "IssueAppChallenge");

        Assert.Equal((typeof(IssueAppChallengeRequest), ActionClass.Desk, "POST", "/sindri/workflow/challenges/app", "ready", false, false, null),
            (row.CommandType, row.Class, row.Method, row.Route, row.Ui, row.NeedsTerminal, row.AppConfirm, row.Confirm));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ApproveProfile")]
    [InlineData("ApproveRecipe")]
    [InlineData("ApproveAllRecipes")]
    public void The_app_the_terminal_and_a_session_pass_and_a_service_and_no_caller_are_refused(string action)
    {
        var row = Assert.Single(Rows, r => r.Action.Value == action);

        Assert.True(Table.Decide(row.Class, A.Caller().Human().Client(ClientLabel.Ui).Build(), row).Allowed);
        Assert.True(Table.Decide(row.Class, A.Caller().AtTerminal().Client(ClientLabel.Cli).Build(), row).Allowed);
        Assert.True(Table.Decide(row.Class, A.Caller().Session().Build(), row).Allowed);
        foreach (var caller in new[] { A.Caller().Service("jobs").Build(), null })
        {
            Assert.Equal(SindriErrors.AuthRefused.Code, Table.Decide(row.Class, caller, row).Refusal?.Code);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_command_list_serves_confirm_null_and_names_the_app_and_a_session_as_callers()
    {
        var listed = (await new ListCommandsService(new CommandCatalog()).Handle(new ListCommandsRequest(), TestContext.Current.CancellationToken)).Value.Rows;

        Assert.All(listed, r => Assert.Null(r.Confirm));
        Assert.All(_formerlyConfirmable.Append("RevokeRecipe"), a =>
        {
            var callers = Assert.Single(listed, r => r.Action.Value == a).AllowedCallers;
            Assert.Contains("human", callers, StringComparer.Ordinal);
            Assert.Contains("session", callers, StringComparer.Ordinal);
        });
    }
}
