using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="IPreferencesStore"/> passes: the in-memory one, and the desktop file store over a temp
/// directory (whose own tests add the 0600 mode and the temp-file-and-rename write).
/// </summary>
public abstract class PreferencesStoreContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A store with nothing in it.</summary>
    /// <returns>The store.</returns>
    protected abstract IPreferencesStore Create();

    [Fact]
    public async Task Nothing_stored_reads_as_null() => Expect.True(await Create().Read(Token) is null);

    [Fact]
    public async Task What_is_written_reads_back_byte_for_byte()
    {
        var store = Create();
        byte[] contents = [.. """{"version":1,"lastPage":"Queue"}"""u8];

        await store.Write(contents, Token);

        Expect.Sequence(contents, await store.Read(Token));
    }

    [Fact]
    public async Task A_second_write_replaces_the_first_whole()
    {
        var store = Create();

        await store.Write([.. """{"version":1,"lastPage":"Queue","seenIds":["a","b","c"]}"""u8], Token);
        await store.Write([.. "{}"u8], Token);

        Expect.Sequence([.. "{}"u8], await store.Read(Token));
    }

    [Fact]
    public async Task Any_bytes_round_trip_the_app_parses_them_not_the_store()
    {
        var store = Create();
        byte[] damaged = [.. "{ not json"u8, 0xff, 0x00];

        await store.Write(damaged, Token);

        Expect.Sequence(damaged, await store.Read(Token));
    }

    [Fact]
    public void It_names_where_it_keeps_them() => Expect.True(!string.IsNullOrWhiteSpace(Create().Location));
}
