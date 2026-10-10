using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>The registry (Packages.cs) matches the solution, so a new package can't skip the architecture rules.</summary>
[Trait("Lane", "unit")]
public sealed class RegistryTests
{
    /// <summary>A production project in the solution that Packages.cs doesn't list.</summary>
    public const string MissingId = "ARCH-REG-001";

    /// <summary>A Packages.cs line with no production project in the solution, or no ProjectReference here.</summary>
    public const string StaleId = "ARCH-REG-002";

    [Fact]
    public void Every_production_project_in_the_solution_is_registered() =>
        Verify.None(Missing(Solution.ProductionProjects, Packages.Production));

    [Fact]
    public void Every_registered_assembly_is_a_production_project_that_loads() =>
        Verify.None(Stale(Solution.ProductionProjects, Packages.Production)
            .Concat(Packages.Production.Where(n => Production.Assemblies.All(a => a.GetName().Name != n))
                .Select(n => new Violation(StaleId, $"{n} doesn't load", AddReference(n)))));

    [Fact]
    public void A_project_missing_from_the_registry_is_reported_with_the_line_to_add() =>
        Verify.Exactly(Missing(["Aw.Kernel", "Aw.Demo.Core"], ["Aw.Kernel"]), MissingId, "Aw.Demo.Core");

    [Fact]
    public void A_registered_name_without_a_project_is_reported() =>
        Verify.Exactly(Stale(["Aw.Kernel"], ["Aw.Kernel", "Aw.Gone.Core"]), StaleId, "Aw.Gone.Core");

    private static IEnumerable<Violation> Missing(IEnumerable<string> projects, IReadOnlyList<string> registered) =>
        projects.Where(p => !registered.Contains(p, StringComparer.Ordinal))
            .Select(p => new Violation(MissingId, $"{p} is in AgenticWorkflow.slnx but not in Packages.cs",
                $"add \"{p}\", to Packages.Production and {AddReference(p)} (/new-package does both)"));

    private static IEnumerable<Violation> Stale(IReadOnlyList<string> projects, IEnumerable<string> registered) =>
        registered.Where(r => !projects.Contains(r, StringComparer.Ordinal))
            .Select(r => new Violation(StaleId, $"{r} is in Packages.cs but no src/ project in AgenticWorkflow.slnx has that name",
                "remove the line, or add the project to the solution"));

    private static string AddReference(string project) =>
        $"a ProjectReference to its .csproj in tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj ({project})";
}
