namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// No service locator (spec §4b.4): the locator types appear only in Host (with Program and generator output; in the
/// small shape the App root namespace and App.Commands) or inside a *ServiceCollectionExtensions class, where a
/// registration factory lambda resolves a decorator's parts. The class-level match is wider than "only the lambda":
/// the rule can't tell a lambda from another member of that class, so review keeps those classes to registrations.
/// ArchUnitNET records a call by the type that declares the called method, so the extension classes behind
/// GetService&lt;T&gt;/GetRequiredService&lt;T&gt; are listed too; a call through a type that implements IServiceProvider
/// (ValidationContext.GetService, for one) binds to that type and isn't caught.
/// </summary>
public static class ServiceProviderRules
{
    /// <summary>A locator type used outside Host and the registration classes.</summary>
    public const string Id = "ARCH-DI-001";

    private static readonly string[] _locators =
    [
        "System.IServiceProvider", "Microsoft.Extensions.DependencyInjection.IKeyedServiceProvider",
        "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory", "Microsoft.Extensions.DependencyInjection.IServiceScope",
        "Microsoft.Extensions.DependencyInjection.AsyncServiceScope", "Microsoft.Extensions.DependencyInjection.ActivatorUtilities",
        "Microsoft.Extensions.DependencyInjection.IServiceProviderIsService",
        "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions",
        "Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions",
    ];

    /// <summary>Whether a type (by full name) is a service-locator handle.</summary>
    /// <param name="fullName">The type's full name.</param>
    /// <returns>True for a locator type.</returns>
    public static bool IsLocator(string fullName) => _locators.Contains(fullName.Split('`')[0], StringComparer.Ordinal);

    /// <summary>Checks every edge to a locator type.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <returns>One violation per offending type and locator.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) =>
        edges
            .Where(e => IsLocator(e.Target.FullName)
                && e.From?.Layer is not (Layer.Host or Layer.Commands)
                && !TypeNames.TopLevel(e.Origin).EndsWith("ServiceCollectionExtensions", StringComparison.Ordinal))
            .Select(e => new Violation(Id, $"{TypeNames.Qualified(e.Origin)} uses {e.Target.Name}",
                "take the dependency through the constructor; only a registration factory lambda inside the project's own "
                + "Add…() method (a *ServiceCollectionExtensions class) may resolve from IServiceProvider"));
}
