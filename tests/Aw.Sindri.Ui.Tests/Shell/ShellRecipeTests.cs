using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The recipe sheet in the shell (U10a): a recipe link claims the sheet from the placeholder, names its recipe at once and keeps the widest width.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellRecipeTests
{
    private const string Id = "workflow.dev-desk-round-trip";

    private static async Task<ShellViewModel> Shell()
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(TestContext.Current.CancellationToken);
        return shell;
    }

    private static DeepLink Link(string text) => DeepLink.TryParse(text, out var link) ? link : throw new InvalidOperationException(text);

    [Fact]
    public async Task Deep_link_recipe_opens_the_recipe_sheet_not_the_placeholder()
    {
        var shell = await Shell();

        shell.Navigate(Link("recipe/" + Id));

        Assert.Equal((true, false, Id, FramePhase.Loading), (shell.IsRecipeSheet, shell.IsSheetPlaceholderShown, shell.Recipe.Id, shell.Recipe.Phase));
        Assert.False(shell.IsInstanceSheet);
        Assert.Equal("Recipe " + Id, shell.SheetName);
    }

    [Fact]
    public async Task A_recipe_link_with_a_hash_names_the_recipe_alone()
    {
        var shell = await Shell();

        shell.Navigate(Link("recipe/" + Id + "@b77152c8613e"));

        Assert.Equal((true, Id), (shell.IsRecipeSheet, shell.Recipe.Id));
    }

    [Fact]
    public async Task Recipe_sheet_opens_at_760()
    {
        var shell = await Shell();
        shell.Navigate(Link("recipe/" + Id));

        Assert.Equal(UiPreferences.MaxSheetWidth, shell.ShownSheetWidth);
        shell.ResizeSheet(500);
        Assert.Equal(UiPreferences.MaxSheetWidth, shell.ShownSheetWidth);
        shell.Navigate(Link("item/ITM-1"));
        Assert.NotEqual(UiPreferences.MaxSheetWidth, shell.ShownSheetWidth);
    }

    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(TestContext.Current.CancellationToken);
        _ = shell.Run(TestContext.Current.CancellationToken);
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return shell;
    }

    [Fact]
    public Task Frame_recipe_link_stacks_the_recipe_sheet_and_Back_returns_to_the_frame() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var detail = FixtureInstances.Detail("project-build");
        shell.OpenInstance(detail.Address, detail.Id);
        for (var i = 0; i < 400 && shell.Instance.Phase != FramePhase.Shown; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        shell.Instance.OpenRecipe();
        for (var i = 0; i < 400 && shell.Recipe.Phase != FramePhase.Shown; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal((true, true, detail.Recipe.Id, FramePhase.Shown), (shell.IsRecipeSheet, shell.HasTrail, shell.Recipe.Id, shell.Recipe.Phase));
        Assert.Equal((detail.Recipe.Id, detail.Recipe.Hash["sha256:".Length..][..12]), _server.LastRecipeRead);
        shell.Back();
        Assert.Equal((true, false, detail.Address), (shell.IsInstanceSheet, shell.HasTrail, shell.Instance.Address));
        Assert.Equal(FrameFocus.Recipe, shell.Instance.WantedFocus?.Target);   // Back lands on the link that opened the recipe, not on a control that has hidden
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Group_head_recipe_button_opens_the_approved_recipe_with_no_trail() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.GoTo(PageId.Workflows);
        for (var i = 0; i < 400 && shell.Workflows.Groups.Count == 0; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        var group = shell.Workflows.Groups[0];
        group.OpenRecipe();

        Assert.Equal((true, false, group.Recipe), (shell.IsRecipeSheet, shell.HasTrail, shell.Recipe.Id));
        Assert.Null(_server.LastRecipeRead?.Hash);
        Assert.Equal(group.Recipe, shell.OpenerRecipeGroup);   // Esc returns focus to that group's button
        shell.Navigate(Link("item/ITM-1"));
        Assert.Null(shell.OpenerRecipeGroup);
    }, TestContext.Current.CancellationToken);
}
