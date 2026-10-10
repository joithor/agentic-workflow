using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>Red stub.</summary>
public static class NamingRules
{
    /// <summary>A dumping-ground name.</summary>
    public const string Id = "ARCH-NAME-001";

    /// <summary>Red stub.</summary>
    /// <param name="types">The types.</param>
    /// <returns>Nothing yet.</returns>
    public static IEnumerable<Violation> CheckNamespaces(IEnumerable<IType> types) => types.Take(0).Select(_ => new Violation(Id, string.Empty, string.Empty));

    /// <summary>Red stub.</summary>
    /// <param name="projects">Project names.</param>
    /// <returns>Nothing yet.</returns>
    public static IEnumerable<Violation> CheckProjects(IEnumerable<string> projects) => projects.Take(0).Select(_ => new Violation(Id, string.Empty, string.Empty));
}
