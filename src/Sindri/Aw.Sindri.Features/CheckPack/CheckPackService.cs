using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.CheckPack;

/// <summary>
/// 'sindri pack check <dir>' (line spec §5.3): reads the pack's files, checks its schema, that every referenced file is present, the lens count and that nothing points outside the pack,
/// then gives its hash and the pin line to approve. The hash is the lens pack's own (LensPackReader.Hash): aw-packets hashes packets, and a lens pack is not one of its kinds.
/// </summary>
/// <param name="directories">Reads the pack directory.</param>
internal sealed class CheckPackService(IPackDirectories directories) : IService<CheckPackRequest, CheckPackResponse>
{
    private const string Code = "SND-PACK-003";

    /// <summary>The pack id a directory path names: its last segment.</summary>
    /// <param name="directory">The path as given.</param>
    /// <returns>The id.</returns>
    internal static string IdOf(string directory) => Path.GetFileName(directory.TrimEnd('/', '\\'));

    public async ValueTask<Result<CheckPackResponse>> Handle(CheckPackRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var read = await directories.Read(request.Directory, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var id = IdOf(request.Directory);
        List<PackFinding> findings =
        [
            .. read.Value.Outside.Select(static path => new PackFinding(Code, $"{path} points outside the pack (fix: copy the file into the pack, a pack holds only its own files)")),
        ];
        Result<LensPack> pack = LensPackId.TryFrom(id, out var packId)
            ? LensPackReader.Read(packId, read.Value.Files)
            : SindriErrors.InvalidInput.With($"the lens pack '{id}' is not a pack id (a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters; fix: name the directory after the pack id)");
        if (pack.IsError)
        {
            findings.Add(new PackFinding(Code, pack.Error.Message));
        }

        if (findings.Count > 0)
        {
            return new CheckPackResponse(Passed: false, id, pack.IsSuccess ? Applies(pack.Value) : null, pack.IsSuccess ? pack.Value.Lenses.Count : 0, Hash: null, Pin: null, findings);
        }

        return new CheckPackResponse(Passed: true, id, Applies(pack.Value), pack.Value.Lenses.Count, pack.Value.Hash, $"workflow.packs.pins: lenses/{id}: {pack.Value.Hash}", []);
    }

    private static string Applies(LensPack pack) => pack.Applies == LensPackKind.Design ? "design" : "review";
}
