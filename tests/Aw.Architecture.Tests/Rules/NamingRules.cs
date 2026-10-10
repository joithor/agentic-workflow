using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>No dumping grounds (spec §4b.1a): no Common, Shared, Utils or Helpers project or namespace.</summary>
public static class NamingRules
{
    /// <summary>A namespace or project name with a dumping-ground segment.</summary>
    public const string Id = "ARCH-NAME-001";

    private static readonly string[] _banned = ["Common", "Shared", "Utils", "Helpers"];

    /// <summary>Checks every type's namespace.</summary>
    /// <param name="types">The types to check.</param>
    /// <returns>One violation per offending namespace.</returns>
    public static IEnumerable<Violation> CheckNamespaces(IEnumerable<IType> types) =>
        types.Select(t => t.Namespace.FullName).Distinct(StringComparer.Ordinal).Where(IsBanned)
            .Select(ns => new Violation(Id, $"namespace {ns} is a dumping ground", Fix));

    /// <summary>Checks project names.</summary>
    /// <param name="projects">Project names, e.g. Aw.Judge.Core.</param>
    /// <returns>One violation per offending project.</returns>
    public static IEnumerable<Violation> CheckProjects(IEnumerable<string> projects) =>
        projects.Where(IsBanned).Select(p => new Violation(Id, $"project {p} is a dumping ground", Fix));

    private const string Fix = "put the code in the package that owns it (its Core); shared code goes into Aw.Kernel only if "
        + "it is domain-free and stable, otherwise duplicate it until a second case proves the abstraction (§6.2)";

    private static bool IsBanned(string name) => name.Split('.').Any(s => _banned.Contains(s, StringComparer.Ordinal));
}
