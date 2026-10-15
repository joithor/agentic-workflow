using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>What the file store adds to the contract: owner-only modes, and a write that is a temp file and a rename.</summary>
[Lane(TestLane.Integration)]
public sealed class FilePreferencesStoreTests : IDisposable
{
    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Ui => Path.Combine(_dir, "home", "ui");

    private string File => Path.Combine(Ui, "preferences.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static UnixFileMode Mode(string path) => OperatingSystem.IsWindows() ? UnixFileMode.None : System.IO.File.GetUnixFileMode(path);

    [Fact]
    public async Task The_file_is_0600_and_its_directory_0700()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes");

        await new FilePreferencesStore(File).Write([.. "{}"u8], Token);

        Assert.Equal(OwnerReadWrite, Mode(File));
        Assert.Equal(OwnerReadWrite | UnixFileMode.UserExecute, Mode(Ui));
    }

    [Fact]
    public async Task A_file_left_readable_by_others_is_replaced_by_a_0600_one()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes");
        _ = Directory.CreateDirectory(Ui);
        await System.IO.File.WriteAllTextAsync(File, "{}", Token);
        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(File, OwnerReadWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        await new FilePreferencesStore(File).Write([.. """{"version":1}"""u8], Token);

        Assert.Equal(OwnerReadWrite, Mode(File));
    }

    [Fact]
    public async Task A_write_leaves_only_the_file_behind()
    {
        var store = new FilePreferencesStore(File);

        await store.Write([.. "{}"u8], Token);
        await store.Write([.. """{"version":1}"""u8], Token);

        Assert.Equal(["preferences.json"], Directory.GetFileSystemEntries(Ui).Select(Path.GetFileName), StringComparer.Ordinal);
    }

    [Fact]
    public void Its_location_is_the_file() => Assert.Equal(File, new FilePreferencesStore(File).Location);
}
