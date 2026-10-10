using Aw.Kernel;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>The file-system adapter of <see cref="IRepoReader"/>.</summary>
internal sealed class FileSystemRepoReader : IRepoReader
{
    public Result<IRepoTree> Open(string root) => Directory.Exists(root)
        ? new FileSystemRepoTree(Path.GetFullPath(root))
        : ReadinessErrors.RootNotFound.With($"{root} is not a directory; fix: run from the repo root or pass --root <checkout>");
}
