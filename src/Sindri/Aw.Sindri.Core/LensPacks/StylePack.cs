namespace Aw.Sindri.Core.LensPacks;

/// <summary>A resolved style pack (line spec §5.1): its id, the hash of its files, its sections in order and its wording rules.</summary>
/// <param name="Id">The pack id.</param>
/// <param name="Hash">'sha256:' and 64 hex digits over the pack's files, as for a lens pack.</param>
/// <param name="Sections">The sections spec.md follows, in order.</param>
/// <param name="Rules">The wording rules.</param>
public sealed record StylePack(LensPackId Id, string Hash, IReadOnlyList<StyleSection> Sections, StyleRules Rules);
