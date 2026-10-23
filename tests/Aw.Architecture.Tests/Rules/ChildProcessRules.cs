using System.Diagnostics;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// Every child process of Sindri's server assemblies is started through one factory (deployable-context spec A2d), so the pod-mode environment
/// allowlist cannot be bypassed: no type but the factory constructs a ProcessStartInfo. Read from the IL (newobj), so a lambda or an async method counts.
/// </summary>
public static class ChildProcessRules
{
    /// <summary>A type other than the factory constructs a ProcessStartInfo.</summary>
    public const string Id = "ARCH-PROC-001";

    /// <summary>Checks the types.</summary>
    /// <param name="types">Every type of the assemblies in scope (nested types are read through their owners).</param>
    /// <param name="factoryName">The factory type's name, the one type that may construct a ProcessStartInfo.</param>
    /// <returns>One violation per offending type.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Type> types, string factoryName)
    {
        ArgumentNullException.ThrowIfNull(types);
        return types.Where(static t => !t.IsNested && t.Name != "<Module>")
            .Where(t => t.Name != factoryName && IlScan.ConstructedTypes(t).Contains(typeof(ProcessStartInfo)))
            .Select(t => new Violation(Id, $"{t.FullName} constructs a ProcessStartInfo",
                $"start the process with {factoryName}.Start(...) (Infrastructure/Processes), which applies the pod-mode child environment"));
    }
}
