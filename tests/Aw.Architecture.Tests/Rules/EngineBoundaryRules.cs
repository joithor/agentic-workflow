namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// The Temporal boundary (API spec §3.8, P13, E-30): the app, the CLI, the UI and the MCP server hold no Temporal client and learn no Temporal
/// address, so only the Temporal adapter project (the worker and the engine read adapter, <see cref="AllowedProject"/>) references the SDK
/// package and only its types use a type of it. The spec's wording is "no project but this one"; the worker lives in the adapter project, so
/// the rule is "no project but the adapter".
/// </summary>
public static class EngineBoundaryRules
{
    /// <summary>The rule id.</summary>
    public const string Id = "ARCH-ENGINE-001";

    /// <summary>The one project that references the Temporal SDK.</summary>
    public const string AllowedProject = "Aw.Sindri.Workflows";

    /// <summary>The namespace its types sit in.</summary>
    public const string AllowedNamespace = "Aw.Sindri.Infrastructure.Workflows";

    /// <summary>The Temporal SDK's package name.</summary>
    public const string Package = "Temporalio";

    /// <summary>Checks which project references the package.</summary>
    /// <param name="projects">Each production project and the packages it references.</param>
    /// <returns>One violation per project but the adapter that references the SDK.</returns>
    public static IEnumerable<Violation> CheckPackages(IEnumerable<(string Project, IReadOnlyList<string> Packages)> projects) => throw new NotSupportedException();

    /// <summary>Checks which types use a Temporal SDK type.</summary>
    /// <param name="uses">Each (using type, used type) pair, by full name.</param>
    /// <returns>One violation per using type outside the adapter's namespace.</returns>
    public static IEnumerable<Violation> CheckTypes(IEnumerable<(string Origin, string Target)> uses) => throw new NotSupportedException();
}
