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

        shell.Navigate(Link("recipe/" + Id + "@94c0c28e21ca"));

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
}
