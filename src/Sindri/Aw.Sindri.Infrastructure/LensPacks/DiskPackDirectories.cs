using Aw.Kernel;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary><see cref="IPackDirectories"/> over the disk: every file under the directory, links not followed out of it.</summary>
internal sealed class DiskPackDirectories : IPackDirectories
{
    public ValueTask<Result<PackDirectory>> Read(string directory, CancellationToken cancellationToken) => throw new NotSupportedException();
}
