using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.CheckPack;

/// <summary>
/// 'sindri pack check <dir>' (line spec §5.3): reads the pack's files, checks its schema, that every referenced file is present, the lens count and that nothing points outside the pack,
/// then gives its hash and the pin line to approve. The hash is the lens pack's own (LensPackReader.Hash): aw-packets hashes packets, and a lens pack is not one of its kinds.
/// </summary>
/// <param name="directories">Reads the pack directory.</param>
internal sealed class CheckPackService(IPackDirectories directories) : IService<CheckPackRequest, CheckPackResponse>
{
    /// <summary>The pack id a directory path names: its last segment.</summary>
    /// <param name="directory">The path as given.</param>
    /// <returns>The id.</returns>
    internal static string IdOf(string directory) => throw new NotSupportedException();

    public ValueTask<Result<CheckPackResponse>> Handle(CheckPackRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
