namespace Aw.Sindri.Core.LensPacks;

/// <summary>A resolved lens pack (line spec §5.2): its id, the hash of its files and its lenses in pack order.</summary>
/// <param name="Id">The pack id.</param>
/// <param name="Hash">'sha256:' and 64 hex digits over the pack's files: each path and the hash of its bytes, in path order.</param>
/// <param name="Applies">The loop the pack serves.</param>
/// <param name="Lenses">The lenses, 1 to <see cref="MaxLenses"/>, in pack order.</param>
public sealed record LensPack(LensPackId Id, string Hash, LensPackKind Applies, IReadOnlyList<PackLens> Lenses)
{
    /// <summary>The most lenses a pack may hold, so a run's lens loop is bounded (line spec §5.2).</summary>
    public const int MaxLenses = 10;
}
