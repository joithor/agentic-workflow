using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Platform;

/// <summary>UI spec §12.3: the app reads its preferences clamped and resets them when unreadable, whatever the head stores.</summary>
[Lane(TestLane.Unit)]
public sealed class PreferencesTests
{
    private readonly InMemoryPreferencesStore _store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static UiPreferences Sample => UiPreferences.Defaults with
    {
        Window = new WindowBounds(40, 60, 1280, 800),
        SheetWidth = 600,
        LastPage = "Work",
        SeenIds = ["nyr-1", "nyr-2"],
        PaletteHistory = ["Rebuild views"],
        TextScale = 1.2,
        ReduceMotion = true,
    };

    [Fact]
    public async Task Nothing_stored_reads_as_the_defaults() => Assert.Equal(UiPreferences.Defaults, await new Preferences(_store).Load(Token));

    [Fact]
    public async Task What_is_saved_loads_back()
    {
        var preferences = new Preferences(_store);

        await preferences.Update(_ => Sample);

        Assert.Equal(Sample, await preferences.Load(Token));
    }

    [Fact]
    public async Task Values_out_of_range_are_saved_and_loaded_clamped()
    {
        var preferences = new Preferences(_store);

        await preferences.Update(_ => Sample with { Window = new WindowBounds(0, 0, 300, 200), SheetWidth = 2000, TextScale = 9, LastPage = "Nowhere" });

        var read = await preferences.Load(Token);
        Assert.Equal(new WindowBounds(0, 0, UiPreferences.MinWidth, UiPreferences.MinHeight), read.Window);
        Assert.Equal((UiPreferences.MaxSheetWidth, UiPreferences.MaxTextScale, UiPreferences.DefaultPage), (read.SheetWidth, read.TextScale, read.LastPage));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"version":2,"sheetWidth":600,"lastPage":"Work","seenIds":[],"paletteHistory":[],"textScale":1.1,"reduceMotion":true}""")]
    public async Task A_malformed_or_foreign_file_loads_as_the_defaults(string text)
    {
        await _store.Write(Encoding.UTF8.GetBytes(text), Token);

        Assert.Equal(UiPreferences.Defaults, await new Preferences(_store).Load(Token));
    }
}
