using Aw.Kernel;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>The pack lane's error catalogue (line spec Appendix D, LN-PK): SND-PACK-001 to -004, validated with <see cref="SindriErrors"/>.</summary>
public static class PackErrors
{
    /// <summary>SND-PACK-001: no pack of that kind resolves.</summary>
    public static readonly ErrorDefinition NotFound = new("SND-PACK-001", ErrorKind.NotFound, "No pack of that kind resolves");

    /// <summary>SND-PACK-002: a repo or machine pack has no pin, or its hash differs from it.</summary>
    public static readonly ErrorDefinition PinMismatch = new("SND-PACK-002", ErrorKind.Conflict, "A pack has no pin in the profile, or its hash differs from the pin");

    /// <summary>SND-PACK-003: a pack's schema, a missing file, a file outside the pack, or more than 10 lenses.</summary>
    public static readonly ErrorDefinition Invalid = new("SND-PACK-003", ErrorKind.Invalid, "A pack is not valid");

    /// <summary>SND-PACK-004: a pack of the wrong kind or 'applies'.</summary>
    public static readonly ErrorDefinition WrongKind = new("SND-PACK-004", ErrorKind.Invalid, "A pack is of the wrong kind for what asked for it");

    /// <summary>The catalogue's codes.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [NotFound, PinMismatch, Invalid, WrongKind];
}
