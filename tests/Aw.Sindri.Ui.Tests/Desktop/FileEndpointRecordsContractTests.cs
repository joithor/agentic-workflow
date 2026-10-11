using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Integration)]
public sealed class FileEndpointRecordsContractTests : EndpointRecordSourceContract, IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    protected override IEndpointRecordSource With(byte[]? record)
    {
        var path = Path.Combine(_dir, "home", "control.json");
        if (record is not null)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!); // why: Combine above always has a directory
            File.WriteAllBytes(path, record);
        }

        return new FileEndpointRecords(path);
    }
}
