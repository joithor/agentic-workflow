using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Integration)]
public sealed class FilePreferencesStoreContractTests : PreferencesStoreContract, IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    protected override IPreferencesStore Create() => new FilePreferencesStore(Path.Combine(_dir, "home", "ui", "preferences.json"));
}
