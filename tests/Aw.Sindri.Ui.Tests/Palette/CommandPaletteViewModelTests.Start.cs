using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>The Start workflow entry (<see cref="CommandPaletteViewModelTests"/>, continued; U5').</summary>
public sealed partial class CommandPaletteViewModelTests
{
    private CatalogViewModel CatalogFromGolden()
    {
        var controls = new CatalogViewModel(_clipboard, _clock);
        controls.Show(_api, _server, AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value);
        return controls;
    }

    [Fact]
    public async Task Start_workflow_opens_the_form_and_closes_the_palette_when_the_catalog_says_ready()
    {
        var started = 0;
        var palette = new CommandPaletteViewModel(CatalogFromGolden(), _pages.Add, _history.Add, startWorkflow: () => started++);
        palette.Open();
        var entry = palette.Entries.Single(e => e.Id == "StartInstance");

        await palette.Run(entry, Token);

        Assert.Equal((true, Labels.Ready, 1, false), (entry.IsEnabled, entry.Status, started, palette.IsOpen));
        Assert.Equal(["StartInstance"], _history);
        Assert.Empty(_pages);
    }

    [Fact]
    public void Start_workflow_without_a_form_to_open_is_not_runnable()
    {
        var palette = new CommandPaletteViewModel(CatalogFromGolden(), _pages.Add, _history.Add);
        palette.Open();

        Assert.False(palette.Entries.Single(e => e.Id == "StartInstance").IsEnabled);
    }
}
