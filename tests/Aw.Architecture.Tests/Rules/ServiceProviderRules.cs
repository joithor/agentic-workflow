namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// No service locator (spec §4b.4): IServiceProvider appears only in Host (and in the small shape's Composition and
/// Commands), or in a registration factory lambda inside a project's own *ServiceCollectionExtensions class.
/// </summary>
public static class ServiceProviderRules
{
    /// <summary>IServiceProvider used outside Host and the registration classes.</summary>
    public const string Id = "ARCH-DI-001";

    /// <summary>Checks every edge to System.IServiceProvider.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <returns>One violation per offending type.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) =>
        edges
            .Where(e => e.Target.FullName == "System.IServiceProvider"
                && e.From?.Layer is not (Layer.Host or Layer.Commands)
                && !TypeNames.TopLevel(e.Origin).EndsWith("ServiceCollectionExtensions", StringComparison.Ordinal))
            .Select(e => new Violation(Id, $"{TypeNames.Qualified(e.Origin)} uses IServiceProvider",
                "take the dependency through the constructor; only a registration factory lambda inside the project's own "
                + "Add…() method (a *ServiceCollectionExtensions class) may resolve from IServiceProvider"));
}
