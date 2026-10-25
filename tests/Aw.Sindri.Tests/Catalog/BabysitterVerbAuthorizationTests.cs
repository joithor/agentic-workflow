using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// PB spec §13 (PB-E): the babysitter's 'ready' state takes 'merge' and 'rework' as recipe verbs, and both are served by the one generic desk row 'ApplyCommand:command' (owner ruling), so an agent
/// session, a human in the app and a human at a terminal are all let through (owner decision q93).
/// </summary>
public sealed class BabysitterVerbAuthorizationTests
{
    private static readonly CommandCatalog _catalog = new();

    [Theory, Trait("Lane", "unit")]
    [InlineData("merge")]
    [InlineData("rework")]
    public void A_session_that_sends_a_ready_verb_is_let_through_like_a_human(string verb)
    {
        var recipe = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.pr-babysitter.yaml"), "workflow.pr-babysitter.yaml").Document!.Recipe;
        Assert.Contains(recipe.Transitions, t => t.From.Contains("ready", StringComparer.Ordinal) && string.Equals(t.On, "command." + verb, StringComparison.Ordinal));
        var row = ActionClassifier.Classify(_catalog, new ApplyCommandRequest("T-3/pr-babysitter/1", ApplyCommandKind.Command, Verb: CommandVerb.From(verb)));

        var session = Authorization.Decide(row.Class, A.Caller().Session().Build(), row);
        var app = Authorization.Decide(row.Class, A.Caller().Human().Client(ClientLabel.Ui).Build(), row);
        var terminal = Authorization.Decide(row.Class, A.Caller().AtTerminal().Client(ClientLabel.Cli).Build(), row);

        Assert.Equal("ApplyCommand:command", row.Action.Value);
        Assert.Null(session.Refusal);
        Assert.Null(app.Refusal);
        Assert.Null(terminal.Refusal);
    }
}
