using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>Red stub.</summary>
public static class CycleRules
{
    /// <summary>A namespace cycle.</summary>
    public const string Id = "ARCH-CYCLE-001";

    /// <summary>Red stub.</summary>
    /// <param name="types">The types.</param>
    /// <returns>Nothing yet.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<IType> types) => types.Take(0).Select(_ => new Violation(Id, string.Empty, string.Empty));
}
