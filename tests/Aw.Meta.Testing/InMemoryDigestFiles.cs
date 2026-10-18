using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IDigestFiles: the one fake, kept honest by DigestFilesContract.</summary>
public sealed class InMemoryDigestFiles : IDigestFiles
{
    private readonly Dictionary<string, DigestInfo> _files = new(StringComparer.Ordinal);

    /// <summary>Puts a digest in place.</summary>
    /// <param name="path">The path.</param>
    /// <param name="length">Its size in bytes.</param>
    /// <param name="modifiedAt">When it was written.</param>
    public void Put(string path, long length, DateTimeOffset modifiedAt) => _files[path] = new DigestInfo(length, modifiedAt);

    /// <inheritdoc />
    public ValueTask<Result<DigestInfo?>> Stat(string path, CancellationToken cancellationToken) => Done.Of(_files.GetValueOrDefault(path));
}
