using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Table = Aw.Sindri.Core.Commands.Authorization;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The three rows the app confirms (setup-once spec §4 item 7, §7.4): the AppConfirm flag, the confirm column, and what the table does with each caller on them.</summary>
public sealed class AppConfirmRowTests
{
    private static readonly (string Action, string? Confirm)[] _confirmable = [("ApproveProfile", "typed"), ("ApproveRecipe", "typed"), ("ApproveAllRecipes", "held")];

    private static IReadOnlyList<CommandRow> Rows { get; } = new CommandCatalog().Rows;

    [Fact, Trait("Lane", "unit")]
    public void Exactly_three_rows_carry_the_flag_and_confirm_is_set_on_those_only()
    {
        var flagged = Rows.Where(r => r.AppConfirm).Select(r => (r.Action.Value, r.Confirm)).OrderBy(static r => r.Value, StringComparer.Ordinal).ToArray();

        Assert.Equal(_confirmable.OrderBy(static r => r.Action, StringComparer.Ordinal).ToArray(), flagged);
        Assert.All(Rows.Where(r => !r.AppConfirm), r => Assert.Null(r.Confirm));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_three_rows_are_ready_in_the_app() => Assert.All(Rows.Where(r => r.AppConfirm), r => Assert.Equal("ready", r.Ui));

    [Fact, Trait("Lane", "unit")]
    public void The_app_challenge_is_a_desk_row_for_a_human()
    {
        var row = Assert.Single(Rows, r => r.Action.Value == "IssueAppChallenge");

        Assert.Equal((typeof(IssueAppChallengeRequest), ActionClass.Desk, "POST", "/sindri/workflow/challenges/app", "ready", false, false, null),
            (row.CommandType, row.Class, row.Method, row.Route, row.Ui, row.NeedsTerminal, row.AppConfirm, row.Confirm));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_flagged_rows_are_the_three_requests()
    {
        var types = Rows.Where(r => r.AppConfirm).Select(r => r.CommandType).Order(Comparer<Type?>.Create(static (a, b) => string.CompareOrdinal(a?.Name, b?.Name))).ToArray();

        Assert.Equal([typeof(ApproveAllRecipesRequest), typeof(ApproveProfileRequest), typeof(ApproveRecipeRequest)], types);
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
    public async Task The_command_list_carries_confirm_and_names_the_app_as_a_caller_of_the_three()
    {
        var listed = (await new ListCommandsService(new CommandCatalog()).Handle(new ListCommandsRequest(), TestContext.Current.CancellationToken)).Value.Rows;

        Assert.All(_confirmable, c =>
        {
            var row = Assert.Single(listed, r => r.Action.Value == c.Action);
            Assert.Equal(c.Confirm, row.Confirm);
            Assert.Contains("human", row.AllowedCallers, StringComparer.Ordinal);
            Assert.DoesNotContain("session", row.AllowedCallers, StringComparer.Ordinal);
        });
        Assert.All(listed.Where(r => !_confirmable.Any(c => string.Equals(c.Action, r.Action.Value, StringComparison.Ordinal))), r => Assert.Null(r.Confirm));
        Assert.DoesNotContain("human", Assert.Single(listed, r => r.Action.Value == "RevokeRecipe").AllowedCallers, StringComparer.Ordinal);
    }
}
