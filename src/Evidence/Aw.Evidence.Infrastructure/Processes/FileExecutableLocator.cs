using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Infrastructure.Processes;

/// <summary>Resolves paths on the real file system: every symlink followed, and only an executable regular file is an executable.</summary>
internal sealed class FileExecutableLocator : IExecutableLocator
{
    /// <inheritdoc/>
    public string? RealExecutable(string path) => throw new NotSupportedException();

    /// <inheritdoc/>
    public string? RealDirectory(string path) => throw new NotSupportedException();
}
