using System.Text;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract every <see cref="IPreferencesStore"/> passes: the in-memory one, and the desktop file store over a temp directory.</summary>
public abstract class PreferencesStoreContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A store with nothing in it.</summary>
    /// <returns>The store.</returns>
    protected abstract IPreferencesStore Create();

    /// <summary>Puts raw bytes where <paramref name="store"/> keeps its preferences (a damaged or foreign file).</summary>
    /// <param name="store">A store from <see cref="Create"/>.</param>
    /// <param name="bytes">The bytes.</param>
    protected abstract void WriteRaw(IPreferencesStore store, byte[] bytes);

    private static UiPreferences Sample => UiPreferences.Defaults with
    {
        Window = new WindowBounds(40, 60, 1280, 800),
        SheetWidth = 600,
        LastPage = "Queue",
        SeenIds = ["nyr-1", "nyr-2"],
        PaletteHistory = ["Rebuild views"],
        TextScale = 1.2,
        ReduceMotion = true,
    };

    [Fact]
    public async Task Nothing_stored_reads_as_the_defaults() => Expect.Equal(UiPreferences.Defaults, await Create().Read(Token));

    [Fact]
    public async Task What_is_written_reads_back()
    {
        var store = Create();

        await store.Write(Sample, Token);

        Expect.Equal(Sample, await store.Read(Token));
    }

    [Fact]
    public async Task A_second_write_replaces_the_first()
    {
        var store = Create();

        await store.Write(Sample, Token);
        await store.Write(UiPreferences.Defaults with { LastPage = "Health" }, Token);

        Expect.Equal(UiPreferences.Defaults with { LastPage = "Health" }, await store.Read(Token));
    }

    [Fact]
    public async Task Values_out_of_range_read_clamped()
    {
        var store = Create();

        await store.Write(Sample with { Window = new WindowBounds(0, 0, 300, 200), SheetWidth = 2000, TextScale = 9, LastPage = "Nowhere" }, Token);

        var read = await store.Read(Token);
        Expect.Equal(new WindowBounds(0, 0, UiPreferences.MinWidth, UiPreferences.MinHeight), read.Window);
        Expect.Equal((UiPreferences.MaxSheetWidth, UiPreferences.MaxTextScale, UiPreferences.DefaultPage), (read.SheetWidth, read.TextScale, read.LastPage));
    }

    [Fact]
    public async Task A_malformed_file_reads_as_the_defaults()
    {
        var store = Create();
        WriteRaw(store, Encoding.UTF8.GetBytes("{ not json"));

        Expect.Equal(UiPreferences.Defaults, await store.Read(Token));
    }

    [Fact]
    public async Task A_file_of_another_version_reads_as_the_defaults()
    {
        var store = Create();
        WriteRaw(store, Encoding.UTF8.GetBytes("""{"version":2,"sheetWidth":600,"lastPage":"Queue","seenIds":[],"paletteHistory":[],"textScale":1.1,"reduceMotion":true}"""));

        Expect.Equal(UiPreferences.Defaults, await store.Read(Token));
    }
}
