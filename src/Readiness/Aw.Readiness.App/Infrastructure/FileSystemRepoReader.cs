using Aw.Kernel;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>The file-system adapter of <see cref="IRepoReader"/>.</summary>
internal sealed class FileSystemRepoReader : IRepoReader
{
    public Result<IRepoTree> Open(string root) => throw new NotSupportedException("red: FileSystemRepoReader");
}
