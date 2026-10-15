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

    // Bound to the real cases: a directory where control.json goes, and a file with mode 000 (none on Windows, and
    // root reads it anyway). An unnamed I/O error can't be made on purpose here.
    protected override IEndpointRecordSource? Unreadable(EndpointRecordFailure why)
    {
        var path = Path.Combine(_dir, "home", "control.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!); // why: Combine above always has a directory
        if (why == EndpointRecordFailure.NotAFile)
        {
            _ = Directory.CreateDirectory(path);
            return new FileEndpointRecords(path);
        }

        if (why != EndpointRecordFailure.NoPermission || OperatingSystem.IsWindows())
        {
            return null;
        }

        File.WriteAllBytes(path, [.. "{}"u8]);
        File.SetUnixFileMode(path, UnixFileMode.None);
        return Readable(path) ? null : new FileEndpointRecords(path);
    }

    private static bool Readable(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
