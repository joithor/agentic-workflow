using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IDigestFiles over the file system.</summary>
internal sealed class FileDigestFiles : IDigestFiles
{
    public ValueTask<Result<DigestInfo?>> Stat(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        var info = new FileInfo(path);
        return ValueTask.FromResult(Results.Success(info.Exists ? new DigestInfo(info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)) : null));
    }
}
