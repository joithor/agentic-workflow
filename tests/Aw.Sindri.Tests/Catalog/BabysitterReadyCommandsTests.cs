using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// PR babysitter spec API18: the babysitter's 'ready' state takes 'comments', 'resync-docs' and 'check' as recipe verbs ('on: command.&lt;verb&gt;'), and the
/// one generic 'ApplyCommand:command' row (desk, not destructive; owner ruling) serves every recipe verb, so they need no catalog rows of their own.
/// </summary>
public sealed class BabysitterReadyCommandsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_ready_state_offers_comments_resync_docs_and_check_as_verbs_the_one_desk_command_row_serves()
    {
        var recipe = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.pr-babysitter.yaml"), "workflow.pr-babysitter.yaml").Document!.Recipe;   // why: the shipped recipes bind (ShippedRecipesTests)
        var verbs = recipe.Transitions
            .Where(t => t.From.Contains("ready", StringComparer.Ordinal) && t.On.StartsWith("command.", StringComparison.Ordinal))
            .Select(t => t.On["command.".Length..])
            .ToList();
        var rows = new CommandCatalog().Rows;

        Assert.Contains("comments", verbs, StringComparer.Ordinal);
        Assert.Contains("resync-docs", verbs, StringComparer.Ordinal);
        Assert.Contains("check", verbs, StringComparer.Ordinal);
        var row = Assert.Single(rows, r => r.Action.Value == "ApplyCommand:command");
        Assert.Equal((ActionClass.Desk, false), (row.Class, row.Destructive));
        Assert.DoesNotContain(rows, r => r.Action.Value.StartsWith("ApplyCommand:command-", StringComparison.Ordinal));
    }
}
